namespace BpMonitor.Charts

open System.Globalization
open Plotly.NET
open Plotly.NET.LayoutObjects
open BpMonitor.Core

module BpChart =
  // ── palette ──────────────────────────────────────────────────────────────
  let private transparent = Color.fromString "rgba(0,0,0,0)"
  let private lightGridLine = Color.fromString "rgba(0,0,0,0.08)"

  // Each series has one RGB source of truth; every other shade (full-strength line,
  // faded line, background band) is a derived alpha of it, so retuning a series' hue
  // only ever needs a change in one place.
  let private opaque (r, g, b) =
    Color.fromString $"#%02X{r}%02X{g}%02X{b}"

  let private withAlpha (alpha: float) (r, g, b) =
    Color.fromString $"rgba(%d{r},%d{g},%d{b},%g{alpha})"

  let private systolicRgb = (0, 132, 113)
  let private diastolicRgb = (156, 101, 43)

  let private systolicColor = opaque systolicRgb
  let private diastolicColor = opaque diastolicRgb

  // The /recent chart fades the raw per-reading line, so the LOWESS trend line (kept at
  // full strength) stands out as the visual focus — Wegier et al. 2021, "Smoothing data".
  let private systolicFadedColor = withAlpha 0.22 systolicRgb
  let private diastolicFadedColor = withAlpha 0.22 diastolicRgb

  let private systolicBandColor = withAlpha 0.12 systolicRgb
  let private diastolicBandColor = withAlpha 0.12 diastolicRgb

  // Wegier et al. 2021 Fig. 5's dark-red "Notes" marker (sampled from the figure).
  let private commentRgb = (139, 0, 0)
  let private commentColor = opaque commentRgb

  /// Full-width horizontal background bands behind the data, one per series, matching
  /// the series' color (the "like-with-like" goal-range design from Wegier et al. 2021).
  let private goalBands (goal: GoalRange) : Shape seq =
    let band y0 y1 fillColor =
      Shape.init (
        ShapeType = StyleParam.ShapeType.Rectangle,
        Xref = "paper",
        X0 = 0.,
        X1 = 1.,
        Y0 = float y0,
        Y1 = float y1,
        FillColor = fillColor,
        Line = Line.init (Width = 0.),
        Layer = StyleParam.Layer.Below
      )

    [ band goal.SystolicMin goal.SystolicMax systolicBandColor
      band goal.DiastolicMin goal.DiastolicMax diastolicBandColor ]

  let private marginWithBottom bottom =
    Margin.init (Left = 48, Right = 16, Top = 24, Bottom = bottom)

  // Compact margins: with no chart title, Plotly's default top margin (100) would
  // otherwise sit empty above the plot. Bottom accommodates x-tick labels + the
  // bottom-positioned legend (which sits at Y=-0.15, below the plot area).
  let private compactMargin = marginWithBottom 72

  // Trends rotate x-tick labels at -45°, which extends them further down than horizontal
  // labels — needs a larger bottom margin to fit labels + the legend below them.
  let private trendsMargin = marginWithBottom 96

  // Pre-selects the pan tool in the modebar, so the default drag gesture moves the
  // x-axis window rather than drawing a zoom-box (the y-axis is fixed-range anyway).
  let private layout () =
    Layout.init (
      PaperBGColor = transparent,
      PlotBGColor = transparent,
      Margin = compactMargin,
      DragMode = StyleParam.DragMode.Pan
    )

  // Light-theme default; theme.js relayouts this to the dark-theme font color
  // ("#c2cfd6") on load and on toggle, so it's never stuck unreadable in dark mode.
  let private axisLineColor = Color.fromString "#444"

  // Layer = BelowTraces draws the axis line under the data instead of Plotly's default
  // "above traces" — otherwise it paints over the bottom half of the y=0 comment hexagons
  // (see commentTraces below).
  let private xAxis =
    LinearAxis.init (
      ShowGrid = false,
      ShowLine = true,
      LineColor = axisLineColor,
      Ticks = StyleParam.TickOptions.Outside,
      TickColor = axisLineColor,
      Layer = StyleParam.Layer.BelowTraces
    )

  // Fig. 5's scrubber bar (Wegier et al. 2021, "Scrubber bar"): a vertical line that
  // tracks the cursor's horizontal position, linking the chart to the value strip above
  // it. Plotly's x-axis spikes give us this for free — snapped to the nearest data point
  // (SpikeSnap = Data) and drawn across the whole plot area (SpikeMode = Across).
  // /recent-only: the spike is only meaningful where there's a value strip to link to.
  // Keep in sync with `--color-scrubber` in wwwroot/app.css (the matching scrubber box).
  let private scrubberColor = Color.fromString "#00C853"

  // The chart loads every reading (so panning left reveals older history), but opens
  // focused on the last `windowDays` — set as the axis's initial `Range` rather than
  // relying on autorange, which would zoom out to fit all loaded data instead.
  let private recentXAxis (rangeLow: string) (rangeHigh: string) =
    LinearAxis.init (
      ShowGrid = false,
      ShowLine = true,
      LineColor = axisLineColor,
      Ticks = StyleParam.TickOptions.Outside,
      TickColor = axisLineColor,
      Layer = StyleParam.Layer.BelowTraces,
      Range = StyleParam.Range.ofMinMax (rangeLow, rangeHigh),
      ShowSpikes = true,
      SpikeColor = scrubberColor,
      SpikeThickness = 2,
      SpikeDash = StyleParam.DrawingStyle.Solid,
      SpikeMode = StyleParam.SpikeMode.Across,
      SpikeSnap = StyleParam.SpikeSnap.Data
    )

  // FixedRange disables zoom on this axis — the y-axis is pinned to a clinical 0-200 mmHg
  // scale (the goal-range bands assume it), so zoom/box-select/zoom-in/zoom-out can only
  // ever change the x-axis, never stretch or compress the y-axis out of that scale.
  let private yAxis =
    LinearAxis.init (
      GridColor = lightGridLine,
      Range = StyleParam.Range.MinMax(0, 200),
      FixedRange = true,
      DTick = 20,
      ShowLine = true,
      LineColor = axisLineColor,
      Ticks = StyleParam.TickOptions.Outside,
      TickColor = axisLineColor,
      Title = Title.init (Text = "blood pressure [mmHg]")
    )

  // Plotly's stroke helper sets stroke-opacity as an inline style on every path.yerror
  // (value = alpha of the trace color; for our solid colors that is 1). Normal inline styles
  // beat CSS rules, so a plain CSS dim is always overridden. CSS !important beats normal inline
  // styles, so `stroke-opacity:.1!important` in app.css wins. On hover, we need to win
  // over the CSS !important, which requires setProperty(...,'important') to write an inline
  // !important that takes precedence; removeProperty on unhover falls back to the CSS rule.
  // g.errorbar (singular, per point) lives inside g.errorbars (plural, per trace).
  //
  // /history and /recent are interactive (unlike /trends, which disables the modebar
  // outright): lasso, autoscale and box-select are removed because they let a reader
  // visually distort the blood-pressure scale (Wegier et al. 2021's goal-range bands
  // assume a fixed y-axis).
  let private interactiveConfig =
    Config.init (
      Responsive = true,
      ModeBarButtonsToRemove =
        [ StyleParam.ModeBarButton.Lasso2d
          StyleParam.ModeBarButton.AutoScale2d
          StyleParam.ModeBarButton.Select2d ]
    )

  let private toHtmlString (chart: GenericChart) =
    let html =
      chart
      |> GenericChart.toChartHTML
      |> _.Replace("\"width\":600,", "")
      |> _.Replace("\"height\":600,", "")

    // Prevent script injection: user comment text is serialized into an inline
    // <script> block as JSON. Newtonsoft (used by Plotly.NET) does not escape < or /
    // by default, so a comment containing </script> would close the script element
    // early and allow arbitrary HTML injection into the page.
    //
    // Fix: replace </ → <\/ inside the script content only (not in the surrounding
    // HTML structure). <\/ is valid JSON (RFC 7159 allows \/ as an escape for /) and
    // JS string literals treat \/ as /, so Plotly.js receives the correct value.
    // The HTML parser does not recognize <\ as the start of an end tag.
    let escaped =
      let openTag = html.IndexOf "<script"
      let contentStart = html.IndexOf(">", openTag) + 1
      let closeTag = html.LastIndexOf "</script>"

      if openTag >= 0 && contentStart > 0 && closeTag > contentStart then
        html[.. contentStart - 1]
        + html[contentStart .. closeTag - 1].Replace("</", "<\\/")
        + html[closeTag..]
      else
        html

    escaped

  let private finish (chart: GenericChart) =
    chart
    |> Chart.withLayout (layout ())
    |> Chart.withXAxis xAxis
    |> Chart.withYAxis yAxis
    |> Chart.withConfig interactiveConfig
    |> toHtmlString

  // Like `finish`, but with the scrubber-bar x-axis and unified hover (HoverMode.X finds
  // the nearest point across all traces at the hovered x, not just the one under the
  // cursor) — used only by /recent, which has a value strip to link the spike to.
  let private finishRecentLayout () =
    Layout.init (
      PaperBGColor = transparent,
      PlotBGColor = transparent,
      Margin = compactMargin,
      HoverMode = StyleParam.HoverMode.X,
      DragMode = StyleParam.DragMode.Pan
    )

  /// Horizontal-centered legend below the plot area, shared by /history, /trends and /recent.
  /// Y=-0.15/YAnchor=Top places it below the x-tick labels so they don't collide on mobile.
  let private withBottomLegend (chart: GenericChart) =
    chart
    |> Chart.withLegendStyle (
      Orientation = StyleParam.Orientation.Horizontal,
      X = 0.5,
      XAnchor = StyleParam.XAnchorPosition.Center,
      Y = -0.15,
      YAnchor = StyleParam.YAnchorPosition.Top
    )

  let private finishRecent (rangeLow: string) (rangeHigh: string) (chart: GenericChart) =
    chart
    |> Chart.withLayout (finishRecentLayout ())
    |> Chart.withXAxis (recentXAxis rangeLow rangeHigh)
    |> Chart.withYAxis yAxis
    |> Chart.withConfig interactiveConfig
    |> toHtmlString

  // Trends-specific tuning for mobile:
  // - Compact margins maximize the narrow plot area.
  // - DragMode.False: touch is a tap (tooltip), not a scroll-hijacking pan.
  // - TickAngle=-45: rotated date labels don't overlap on narrow viewports.
  // - Horizontal centred legend avoids stealing horizontal width.
  // - DisplayModeBar=false: no floating toolbar (awkward on touch).
  // - ScrollZoom=NoZoom: wheel/pinch won't fight page scroll.
  let private trendsLayout () =
    Layout.init (
      PaperBGColor = transparent,
      PlotBGColor = transparent,
      Margin = trendsMargin,
      DragMode = StyleParam.DragMode.False
    )

  let private trendsXAxis =
    LinearAxis.init (
      ShowGrid = false,
      TickAngle = -45,
      ShowLine = true,
      LineColor = axisLineColor,
      Ticks = StyleParam.TickOptions.Outside,
      TickColor = axisLineColor
    )

  let private trendsConfig =
    Config.init (Responsive = true, DisplayModeBar = false, ScrollZoom = StyleParam.ScrollZoom.NoZoom)

  let private finishTrends (chart: GenericChart) =
    chart
    |> Chart.withLayout (trendsLayout ())
    |> Chart.withXAxis trendsXAxis
    |> Chart.withYAxis yAxis
    |> Chart.withConfig trendsConfig
    |> withBottomLegend
    |> toHtmlString

  /// Sorts readings chronologically and extracts the parallel label/value series
  /// shared by every per-reading chart (/history and /recent).
  let private seriesOf (readings: BloodPressureReading list) =
    let sorted = readings |> List.sortBy _.Timestamp
    let labels = sorted |> List.map (_.Timestamp >> Formats.formatLocal)
    let systolic = sorted |> List.map _.Systolic
    let diastolic = sorted |> List.map _.Diastolic
    sorted, labels, systolic, diastolic

  /// Comment markers plotted on the x-axis baseline (y=0), one per commented reading —
  /// styled after Wegier et al. 2021 Fig. 5's annotation row: a dark-red hexagon, not
  /// clipped by the x-axis line it sits on (ClipOnAxis = false).
  let private commentTraces (sorted: BloodPressureReading list) : GenericChart list =
    let commented = sorted |> List.filter _.Comments.IsSome

    if commented.IsEmpty then
      []
    else
      let cTimestamps = commented |> List.map (_.Timestamp >> Formats.formatLocal)
      let cBaseline = commented |> List.map (fun _ -> 0)
      let cTexts = commented |> List.map (fun r -> r.Comments |> Option.defaultValue "")

      // A HoverTemplate (rather than HoverInfo.Text) shows the comment itself, then the
      // reading's timestamp dimmed below it; the empty <extra> box drops Plotly's default
      // trace-name prefix ("Comments").
      [ Chart.Point(x = cTimestamps, y = cBaseline, Name = "Comments", MultiText = cTexts)
        |> Chart.withMarkerStyle (Symbol = StyleParam.MarkerSymbol.Hexagon, Size = 11, Color = commentColor)
        |> GenericChart.mapTrace (
          Trace2DStyle.Scatter(
            ClipOnAxis = false,
            HoverTemplate = "%{text}<br><span style=\"opacity:0.6\">%{x}</span><extra></extra>"
          )
        ) ]

  // The trace `Name` ("Systolic"/"Diastolic") is also the color-coded legend entry, so
  // repeating it in every hover tooltip is redundant — these templates keep the
  // measurement(s) but drop the name via the empty `<extra>` box. Each name reflects
  // which fields the owning chart's traces carry: /history and /trends show their own
  // x value per trace; /recent's unified hover (HoverMode.X) already titles the row
  // block with the shared date, so its traces only need the value.
  let private withHoverTemplate (template: string) =
    GenericChart.mapTrace (Trace2DStyle.Scatter(HoverTemplate = template))

  let private hoverXY = withHoverTemplate "%{x}<br>%{y}<extra></extra>"
  let private hoverXYText = withHoverTemplate "%{x}<br>%{y}<br>%{text}<extra></extra>"
  let private hoverYOnly = withHoverTemplate "%{y}<extra></extra>"

  /// Classic x/y plot — one point per reading. Used by /history.
  let private renderIndividual (goal: GoalRange) (readings: BloodPressureReading list) : string =
    let readings, timestamps, systolic, diastolic = seriesOf readings

    [ Chart.Line(x = timestamps, y = systolic, Name = "Systolic", ShowMarkers = true)
      |> Chart.withLineStyle (Color = systolicColor)
      |> hoverXY
      Chart.Line(x = timestamps, y = diastolic, Name = "Diastolic", ShowMarkers = true)
      |> Chart.withLineStyle (Color = diastolicColor)
      |> hoverXY
      yield! commentTraces readings ]
    |> Chart.combine
    |> Chart.withShapes (goalBands goal)
    |> withBottomLegend
    |> finish

  /// Dashed line chart — one point per aggregated period, connected by a dashed line.
  /// Circle marker = single reading in that period; Diamond marker = average of multiple readings.
  /// X-axis labels adapt to granularity: Weekly → date, Monthly → ISO week, Yearly → month name.
  /// Used by /trends for all granularities.
  let private renderDashed (goal: GoalRange) (gran: Granularity) (aggregated: AggregatedReading list) : string =
    let aggregated = aggregated |> List.sortBy _.Reading.Timestamp

    let xLabel (r: BloodPressureReading) =
      let local = r.Timestamp.ToLocalTime()

      match gran with
      | Weekly -> local.Date.ToString("d MMM")
      | Monthly ->
        let week = ISOWeek.GetWeekOfYear(local.Date)
        $"W{week}"
      | Yearly -> local.Date.ToString("MMM")

    let toSymbol count =
      if count = 1 then
        StyleParam.MarkerSymbol.Circle
      else
        StyleParam.MarkerSymbol.Diamond

    let toSysHover (a: AggregatedReading) =
      if a.Count = 1 then
        "1 reading"
      else
        $"{a.Count} readings · {a.MinSystolic}–{a.Reading.Systolic}–{a.MaxSystolic}"

    let toDiaHover (a: AggregatedReading) =
      if a.Count = 1 then
        ""
      else
        $"{a.MinDiastolic}–{a.Reading.Diastolic}–{a.MaxDiastolic}"

    let timestamps = aggregated |> List.map (fun a -> xLabel a.Reading)
    let systolic = aggregated |> List.map _.Reading.Systolic
    let diastolic = aggregated |> List.map _.Reading.Diastolic
    let symbols = aggregated |> List.map (fun a -> toSymbol a.Count)
    let sizes = aggregated |> List.map (fun a -> if a.Count = 1 then 8 else 11)
    let sysHover = aggregated |> List.map toSysHover
    let diaHover = aggregated |> List.map toDiaHover
    let sysUpper = aggregated |> List.map (fun a -> a.MaxSystolic - a.Reading.Systolic)
    let sysLower = aggregated |> List.map (fun a -> a.Reading.Systolic - a.MinSystolic)

    let diaUpper =
      aggregated |> List.map (fun a -> a.MaxDiastolic - a.Reading.Diastolic)

    let diaLower =
      aggregated |> List.map (fun a -> a.Reading.Diastolic - a.MinDiastolic)

    let line name lineColor y text upper lower =
      Chart.Line(
        x = timestamps,
        y = y,
        Name = name,
        LineDash = StyleParam.DrawingStyle.Dash,
        ShowMarkers = true,
        MultiMarkerSymbol = symbols,
        MultiText = text,
        LineWidth = 1.0
      )
      |> Chart.withLineStyle (Color = lineColor)
      |> Chart.withMarkerStyle (MultiSize = sizes, Color = lineColor)
      |> hoverXYText
      |> Chart.withYErrorStyle (
        Visible = true,
        Type = StyleParam.ErrorType.Data,
        Symmetric = false,
        Array = upper,
        Arrayminus = lower,
        Color = lineColor
      )

    [ line "Systolic" systolicColor systolic sysHover sysUpper sysLower
      line "Diastolic" diastolicColor diastolic diaHover diaUpper diaLower ]
    |> Chart.combine
    |> Chart.withShapes (goalBands goal)
    |> finishTrends

  let toHtml (goal: GoalRange) = renderIndividual goal
  let toHtmlDashed (goal: GoalRange) (gran: Granularity) = renderDashed goal gran

  // ── /recent: missing-data-aware solid/dashed line styling ──────────────────
  // Wegier et al. 2021 (docs/resources/12911_2021_Article_1598.pdf, "Missing data"):
  // a gap is "missing data" once the days it skips exceed 10% of the displayed window;
  // such gaps render dashed, ordinary gaps render solid. Consecutive same-style gaps are
  // merged into a single multipoint trace (a "run") instead of one trace per gap, keeping
  // the trace count proportional to the number of dash/solid transitions rather than to N.
  // /recent's load window is wider than its visible focus window (see ReadingHandlers'
  // `recentLoadWindowDays`), so this threshold — tuned for the 30-day focus window — also
  // governs gaps the user only sees by panning back; that's intentional (consistent styling
  // across however much history is loaded), not an oversight.
  let private isGapDashed (windowDays: int) (gapDays: int) =
    let missingDays = gapDays - 1
    float missingDays > 0.10 * float windowDays

  // Calendar-day gap between two readings, in the member's local time zone — using the
  // raw elapsed TotalDays would make the dashed/solid threshold sensitive to the time of
  // day each reading was taken, flipping styling for gaps that are the same number of
  // calendar days apart.
  let private calendarGapDays (r0: BloodPressureReading) (r1: BloodPressureReading) =
    (r1.Timestamp.ToLocalTime().Date - r0.Timestamp.ToLocalTime().Date).Days

  let private dashPattern (windowDays: int) (sorted: BloodPressureReading list) : StyleParam.DrawingStyle list =
    sorted
    |> List.pairwise
    |> List.map (fun (r0, r1) ->
      if isGapDashed windowDays (calendarGapDays r0 r1) then
        StyleParam.DrawingStyle.Dash
      else
        StyleParam.DrawingStyle.Solid)

  // Groups consecutive equal-style gaps into (startPointIdx, endPointIdxInclusive, style)
  // ranges. Adjacent runs share their boundary point, so the connecting line stays unbroken
  // where the style changes.
  let private dashRuns (dashes: StyleParam.DrawingStyle list) : (int * int * StyleParam.DrawingStyle) list =
    dashes
    |> List.indexed
    |> List.fold
      (fun acc (gapIdx, style) ->
        match acc with
        | (s, start, _) :: rest when s = style -> (s, start, gapIdx) :: rest
        | _ -> (style, gapIdx, gapIdx) :: acc)
      []
    |> List.rev
    |> List.map (fun (style, startGap, endGap) -> startGap, endGap + 1, style)

  /// One trace per dash/solid run for a series; only the first run carries the legend
  /// entry, all runs show markers, so every reading still gets a point. Falls back to a
  /// single trace when there's nothing to split (0 or 1 readings).
  let private seriesTraces
    (color: Color)
    (name: string)
    (dashes: StyleParam.DrawingStyle list)
    (labels: string list)
    (values: int list)
    : GenericChart list =
    // The chart's unified hover (HoverMode.X) already titles each row block with the
    // shared date, so a row only needs the value — the color swatch + legend (not
    // repeated here) identify which series it belongs to.
    (match labels, values with
     | [], [] ->
       [ Chart.Line(x = ([]: string list), y = ([]: int list), Name = name)
         |> Chart.withLineStyle (Color = color) ]
     | [ _ ], [ _ ] -> [ Chart.Point(x = labels, y = values, Name = name, MarkerColor = color) ]
     | _ ->
       dashRuns dashes
       |> List.mapi (fun idx (startIdx, endIdx, style) ->
         Chart.Line(
           x = labels[startIdx..endIdx],
           y = values[startIdx..endIdx],
           Name = name,
           ShowLegend = (idx = 0),
           ShowMarkers = true,
           LineDash = style
         )
         |> Chart.withLineStyle (Color = color)))
    |> List.map hoverYOnly

  // Fraction of points in each point's local LOWESS neighborhood. A wide bandwidth
  // (e.g., 0.5) averages over so many points that real local structure — a dip right
  // before a gap, a spike right after one — gets smoothed away into one broad hump,
  // unlike the responsive trend line in Wegier et al. 2021's Fig. 5. A narrow
  // bandwidth keeps the day-to-day jitter damped while still tracking those shifts.
  let private lowessBandwidth = 0.12

  // Minimum reading count for a trend line to be meaningful; below this, a local
  // regression is mostly fitting noise.
  let private minReadingsForTrend = 4

  /// The LOWESS trend overlay for one series — the chart's visual focus, drawn at full
  /// color and thicker than the (faded) raw line, with no markers. Omitted when there
  /// aren't enough readings for the smoothing to be meaningful.
  let private smoothTrace
    (color: Color)
    (name: string)
    (sorted: BloodPressureReading list)
    (labels: string list)
    (values: int list)
    : GenericChart list =
    if List.length values < minReadingsForTrend then
      []
    else
      let first = (List.head sorted).Timestamp.ToLocalTime().Date

      let xs =
        sorted |> List.map (fun r -> (r.Timestamp.ToLocalTime().Date - first).TotalDays)

      let smoothed = Lowess.smooth lowessBandwidth xs (values |> List.map float)

      [ Chart.Line(x = labels, y = smoothed, Name = name, ShowLegend = true)
        |> Chart.withLineStyle (Color = color, Width = 2.5)
        // The smoothed value is derived, not measured — skip its hover entry in the
        // /recent chart's unified hover so the tooltip only ever shows real readings.
        |> GenericChart.mapTrace (Trace2DStyle.Scatter(HoverInfo = StyleParam.HoverInfo.Skip)) ]

  let private renderRecent
    (goal: GoalRange)
    (windowDays: int)
    (windowStart: System.DateTimeOffset)
    (windowEnd: System.DateTimeOffset)
    (readings: BloodPressureReading list)
    : string =
    let readings, timestamps, systolic, diastolic = seriesOf readings
    let dashes = dashPattern windowDays readings
    let rangeLow = Formats.formatLocal windowStart
    let rangeHigh = Formats.formatLocal windowEnd

    [ yield! seriesTraces systolicFadedColor "Systolic" dashes timestamps systolic
      yield! seriesTraces diastolicFadedColor "Diastolic" dashes timestamps diastolic
      yield! smoothTrace systolicColor "Systolic (trend)" readings timestamps systolic
      yield! smoothTrace diastolicColor "Diastolic (trend)" readings timestamps diastolic
      // /recent's unified hover (HoverMode.X) would otherwise surface the comment
      // whenever the cursor is anywhere near its x-column, not just directly on the
      // marker; skipping it here lets recent-scrubber.js drive a custom tooltip that
      // only fires on direct proximity to the hexagon.
      yield!
        commentTraces readings
        |> List.map (GenericChart.mapTrace (Trace2DStyle.Scatter(HoverInfo = StyleParam.HoverInfo.Skip))) ]
    |> Chart.combine
    |> Chart.withShapes (goalBands goal)
    |> withBottomLegend
    |> finishRecent rangeLow rangeHigh

  // `windowStart`/`windowEnd` are computed once by the caller (the same instant the
  // value strip's out-of-range cutoff is computed from) rather than re-derived here from
  // `now`/`windowDays`, so the chart's visible range and the value strip's hidden cells
  // can never drift apart from a duplicated AddDays computation.
  let toHtmlRecent
    (goal: GoalRange)
    (windowDays: int)
    (windowStart: System.DateTimeOffset)
    (windowEnd: System.DateTimeOffset)
    =
    renderRecent goal windowDays windowStart windowEnd
