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
    Color.fromString (sprintf "#%02X%02X%02X" r g b)

  let private withAlpha (alpha: float) (r, g, b) =
    Color.fromString (sprintf "rgba(%d,%d,%d,%g)" r g b alpha)

  let private systolicRgb = (0, 132, 113)
  let private diastolicRgb = (156, 101, 43)

  let private systolicColor = opaque systolicRgb
  let private diastolicColor = opaque diastolicRgb

  // The /recent chart fades the raw per-reading line so the LOWESS trend line (kept at
  // full strength) stands out as the visual focus — Wegier et al. 2021, "Smoothing data".
  let private systolicFadedColor = withAlpha 0.22 systolicRgb
  let private diastolicFadedColor = withAlpha 0.22 diastolicRgb

  let private systolicBandColor = withAlpha 0.12 systolicRgb
  let private diastolicBandColor = withAlpha 0.12 diastolicRgb

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

  // Compact margins, matching the trends chart: with no chart title, Plotly's default
  // top margin (100) would otherwise sit empty above the plot.
  let private compactMargin =
    Margin.init (Left = 48, Right = 16, Top = 24, Bottom = 56)

  let private layout () =
    Layout.init (PaperBGColor = transparent, PlotBGColor = transparent, Margin = compactMargin)

  // Light-theme default; theme.js relayouts this to the dark-theme font color
  // ("#c2cfd6") on load and on toggle, so it's never stuck unreadable in dark mode.
  let private axisLineColor = Color.fromString "#444"

  let private xAxis =
    LinearAxis.init (
      ShowGrid = false,
      ShowLine = true,
      LineColor = axisLineColor,
      Ticks = StyleParam.TickOptions.Outside,
      TickColor = axisLineColor
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
  let private yAxis () =
    let defaultYMin = 0
    let defaultYMax = 200

    LinearAxis.init (
      GridColor = lightGridLine,
      Range = StyleParam.Range.MinMax(defaultYMin, defaultYMax),
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
  // Plotly's initial render ignores the `.chart` container's CSS height (it lays out at its own
  // content-driven default, ~450px) and only correctly fits the actual container on a later
  // resize event. Since `.chart` has `overflow:hidden`, that mismatch clips the bottom of the
  // chart — on narrow mobile heights, severely enough to cut off the x-axis tick labels
  // entirely. Forcing one `Plotly.Plots.resize` right after mount makes it re-measure the real
  // (CSS-constrained) container immediately, instead of waiting for a resize event that may
  // never fire.
  let private errorBarScript =
    "<script>(function(){"
    + "function setup(){"
    + "var d=document.querySelector('.js-plotly-plot');"
    + "if(!d||!d.on){setTimeout(setup,50);return;}"
    + "Plotly.Plots.resize(d);"
    + "d.on('plotly_hover',function(e){"
    + "var p=e.points[0];"
    + "var gs=d.querySelectorAll('g.errorbars')[p.curveNumber];"
    + "if(!gs)return;"
    + "var bar=gs.querySelectorAll('g.errorbar')[p.pointIndex];"
    + "if(!bar)return;"
    + "var path=bar.querySelector('path.yerror');"
    + "if(path)path.style.setProperty('stroke-opacity','1','important');"
    + "});"
    + "d.on('plotly_unhover',function(){"
    + "d.querySelectorAll('g.errorbars path.yerror').forEach(function(p){p.style.removeProperty('stroke-opacity');});"
    + "});"
    + "}"
    + "setTimeout(setup,0);"
    + "})()</script>"

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

  let private finish (chart: GenericChart) =
    chart
    |> Chart.withLayout (layout ())
    |> Chart.withXAxis xAxis
    |> Chart.withYAxis (yAxis ())
    |> Chart.withConfig interactiveConfig
    |> GenericChart.toChartHTML
    |> _.Replace("\"width\":600,", "")
    |> _.Replace("\"height\":600,", "")
    |> fun html -> html + errorBarScript

  // Like `finish`, but with the scrubber-bar x-axis and unified hover (HoverMode.X finds
  // the nearest point across all traces at the hovered x, not just the one under the
  // cursor) — used only by /recent, which has a value strip to link the spike to.
  let private finishRecentLayout () =
    Layout.init (
      PaperBGColor = transparent,
      PlotBGColor = transparent,
      Margin = compactMargin,
      HoverMode = StyleParam.HoverMode.X
    )

  let private finishRecent (rangeLow: string) (rangeHigh: string) (chart: GenericChart) =
    chart
    |> Chart.withLayout (finishRecentLayout ())
    |> Chart.withXAxis (recentXAxis rangeLow rangeHigh)
    |> Chart.withYAxis (yAxis ())
    |> Chart.withConfig interactiveConfig
    |> GenericChart.toChartHTML
    |> _.Replace("\"width\":600,", "")
    |> _.Replace("\"height\":600,", "")
    |> fun html -> html + errorBarScript

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
      Margin = compactMargin,
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
    |> Chart.withYAxis (yAxis ())
    |> Chart.withConfig trendsConfig
    |> Chart.withLegendStyle (
      Orientation = StyleParam.Orientation.Horizontal,
      X = 0.5,
      XAnchor = StyleParam.XAnchorPosition.Center
    )
    |> GenericChart.toChartHTML
    |> _.Replace("\"width\":600,", "")
    |> _.Replace("\"height\":600,", "")
    |> fun html -> html + errorBarScript

  /// Sorts readings chronologically and extracts the parallel label/value series
  /// shared by every per-reading chart (/history and /recent).
  let private seriesOf (readings: BloodPressureReading list) =
    let sorted = readings |> List.sortBy _.Timestamp
    let labels = sorted |> List.map (_.Timestamp >> Formats.formatLocal)
    let systolic = sorted |> List.map _.Systolic
    let diastolic = sorted |> List.map _.Diastolic
    sorted, labels, systolic, diastolic

  /// Comment markers plotted on the x-axis baseline (y=0), one per commented reading.
  let private commentTraces (sorted: BloodPressureReading list) : GenericChart list =
    let commented = sorted |> List.filter _.Comments.IsSome

    if commented.IsEmpty then
      []
    else
      let cTimestamps = commented |> List.map (_.Timestamp >> Formats.formatLocal)
      let cBaseline = commented |> List.map (fun _ -> 0)
      let cTexts = commented |> List.map (fun r -> r.Comments |> Option.defaultValue "")

      [ Chart.Point(x = cTimestamps, y = cBaseline, Name = "Comments", MultiText = cTexts, MarkerColor = systolicColor)
        |> Chart.withMarkerStyle (Size = 10) ]

  /// Classic x/y plot — one point per reading. Used by /history.
  /// `includeHeartRate` is currently always false (no caller passes true).
  let private renderIndividual
    (includeHeartRate: bool)
    (goal: GoalRange)
    (readings: BloodPressureReading list)
    : string =
    let readings, timestamps, systolic, diastolic = seriesOf readings

    let heartRateTrace =
      if includeHeartRate then
        let heartRate = readings |> List.map _.HeartRate
        [ Chart.Line(x = timestamps, y = heartRate, Name = "Heart Rate") ]
      else
        []


    [ Chart.Line(x = timestamps, y = systolic, Name = "Systolic", ShowMarkers = true)
      |> Chart.withLineStyle (Color = systolicColor)
      Chart.Line(x = timestamps, y = diastolic, Name = "Diastolic", ShowMarkers = true)
      |> Chart.withLineStyle (Color = diastolicColor)
      yield! heartRateTrace
      yield! commentTraces readings ]
    |> Chart.combine
    |> Chart.withShapes (goalBands goal)
    |> finish

  type private DailyPoint =
    { Label: string
      Systolic: int
      Diastolic: int
      Symbol: StyleParam.MarkerSymbol
      Size: int
      SysHover: string
      DiaHover: string
      SysUpper: int
      SysLower: int
      DiaUpper: int
      DiaLower: int }

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

    let dailyPoints =
      aggregated
      |> List.map (fun a ->
        let avgSys = a.Reading.Systolic
        let avgDia = a.Reading.Diastolic

        { Label = xLabel a.Reading
          Systolic = avgSys
          Diastolic = avgDia
          Symbol =
            if a.Count = 1 then
              StyleParam.MarkerSymbol.Circle
            else
              StyleParam.MarkerSymbol.Diamond
          Size = if a.Count = 1 then 8 else 11
          SysHover =
            if a.Count = 1 then
              "1 reading"
            else
              $"{a.Count} readings · {a.MinSystolic}–{avgSys}–{a.MaxSystolic}"
          DiaHover =
            if a.Count = 1 then
              ""
            else
              $"{a.MinDiastolic}–{avgDia}–{a.MaxDiastolic}"
          SysUpper = a.MaxSystolic - avgSys
          SysLower = avgSys - a.MinSystolic
          DiaUpper = a.MaxDiastolic - avgDia
          DiaLower = avgDia - a.MinDiastolic })

    let timestamps = dailyPoints |> List.map _.Label
    let systolic = dailyPoints |> List.map _.Systolic
    let diastolic = dailyPoints |> List.map _.Diastolic
    let symbols = dailyPoints |> List.map _.Symbol
    let sizes = dailyPoints |> List.map _.Size
    let sysHover = dailyPoints |> List.map _.SysHover
    let diaHover = dailyPoints |> List.map _.DiaHover
    let sysUpper = dailyPoints |> List.map _.SysUpper
    let sysLower = dailyPoints |> List.map _.SysLower
    let diaUpper = dailyPoints |> List.map _.DiaUpper
    let diaLower = dailyPoints |> List.map _.DiaLower

    // Aggregated readings always have Comments = None, so this trace is empty in practice.
    // Kept for structural completeness.
    let commented = aggregated |> List.filter (fun a -> a.Reading.Comments.IsSome)

    let commentTraces =
      if commented.IsEmpty then
        []
      else
        let cTimestamps = commented |> List.map (fun a -> xLabel a.Reading)
        let cSystolic = commented |> List.map (fun a -> a.Reading.Systolic)

        let cTexts =
          commented |> List.map (fun a -> a.Reading.Comments |> Option.defaultValue "")

        [ Chart.Point(
            x = cTimestamps,
            y = cSystolic,
            Name = "Comments",
            MultiText = cTexts,
            Opacity = 0.5,
            MarkerColor = systolicColor
          ) ]

    let line name lineColor y text upper lower errorColor =
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
      |> Chart.withYErrorStyle (
        Visible = true,
        Type = StyleParam.ErrorType.Data,
        Symmetric = false,
        Array = upper,
        Arrayminus = lower,
        Color = errorColor
      )

    let sysErrorColor = systolicColor
    let diaErrorColor = diastolicColor

    [ line "Systolic" systolicColor systolic sysHover sysUpper sysLower sysErrorColor
      line "Diastolic" diastolicColor diastolic diaHover diaUpper diaLower diaErrorColor
      yield! commentTraces ]
    |> Chart.combine
    |> Chart.withShapes (goalBands goal)
    |> finishTrends

  let toHtml (goal: GoalRange) = renderIndividual false goal
  let toHtmlDashed (goal: GoalRange) (gran: Granularity) = renderDashed goal gran

  // ── /recent: missing-data-aware solid/dashed line styling ──────────────────
  // Wegier et al. 2021 (docs/resources/12911_2021_Article_1598.pdf, "Missing data"):
  // a gap is "missing data" once the days it skips exceed 10% of the displayed window;
  // such gaps render dashed, ordinary gaps render solid. Consecutive same-style gaps are
  // merged into a single multi-point trace (a "run") instead of one trace per gap, keeping
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
  /// entry, all runs show markers so every reading still gets a point. Falls back to a
  /// single trace when there's nothing to split (0 or 1 readings).
  let private seriesTraces
    (color: Color)
    (name: string)
    (dashes: StyleParam.DrawingStyle list)
    (labels: string list)
    (values: int list)
    : GenericChart list =
    match labels, values with
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
        |> Chart.withLineStyle (Color = color))

  // Fraction of points in each point's local LOWESS neighbourhood. A wide bandwidth
  // (e.g. 0.5) averages over so many points that real local structure — a dip right
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
        |> Chart.withLineStyle (Color = color, Width = 3.5)
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
      yield! commentTraces readings ]
    |> Chart.combine
    |> Chart.withShapes (goalBands goal)
    // Horizontal centered legend at the bottom, matching the trends chart, so the recent
    // chart's default top-right legend doesn't steal horizontal width from the plot.
    |> Chart.withLegendStyle (
      Orientation = StyleParam.Orientation.Horizontal,
      X = 0.5,
      XAnchor = StyleParam.XAnchorPosition.Center
    )
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
