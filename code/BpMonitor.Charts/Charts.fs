namespace BpMonitor.Charts

open System.Globalization
open Plotly.NET
open Plotly.NET.LayoutObjects
open BpMonitor.Core

type Theme =
  | Dark
  | Light

module BpChart =
  // ── palette ──────────────────────────────────────────────────────────────
  let private darkBgHex = "#11191f"
  let private darkBg = Color.fromString darkBgHex
  let private transparent = Color.fromString "rgba(0,0,0,0)"
  let private darkFont = Color.fromString "#c2cfd6"
  let private darkGridLine = Color.fromString "rgba(194,207,214,0.12)"
  let private lightGridLine = Color.fromString "rgba(0,0,0,0.08)"
  let private systolicColor = Color.fromString "#16A34A"
  let private diastolicColor = Color.fromString "#EF553B"

  let private layout (theme: Theme) =
    let bg = if theme = Dark then darkBg else transparent

    let font =
      if theme = Dark then
        Font.init (Color = darkFont)
      else
        Font.init ()

    Layout.init (PaperBGColor = bg, PlotBGColor = bg, Font = font)

  let private xAxis = LinearAxis.init (ShowGrid = false)

  let private yAxis (theme: Theme) =
    let gridColor = if theme = Dark then darkGridLine else lightGridLine
    LinearAxis.init (GridColor = gridColor)

  // The chart is rendered inside an iframe (separate document), so the iframe body
  // background must be set explicitly — it does not inherit the parent page theme.
  // `height` is passed in from the caller (read from the ?height= query param set by theme.js,
  // which reads --chart-height from the page CSS) so the value is defined only in app.css.
  let private injectBodyStyle (theme: Theme) (height: string) (html: string) =
    let heightStyle = $"html,body{{height:100%%;margin:0}}body>div{{height:{height}}}"
    // Plotly's stroke helper sets stroke-opacity as an inline style on every path.yerror
    // (value = alpha of the trace color; for our solid colors that is 1). Normal inline styles
    // beat CSS rules, so a plain CSS dim is always overridden. CSS !important beats normal inline
    // styles, so `stroke-opacity:.1!important` in a stylesheet wins. On hover we need to win
    // over the CSS !important, which requires setProperty(...,'important') to write an inline
    // !important that takes precedence; removeProperty on unhover falls back to the CSS rule.
    // g.errorbar (singular, per point) lives inside g.errorbars (plural, per trace).
    let errorBarStyle =
      "g.errorbars path.yerror{stroke-opacity:.1!important;transition:stroke-opacity .15s}"

    let errorBarScript =
      "<script>(function(){"
      + "function setup(){"
      + "var d=document.querySelector('.js-plotly-plot');"
      + "if(!d||!d.on){setTimeout(setup,50);return;}"
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

    let withStyle =
      if theme = Dark then
        html.Replace("</head>", $"<style>body{{background:{darkBgHex}}}{heightStyle}{errorBarStyle}</style></head>")
      else
        html.Replace("</head>", $"<style>{heightStyle}{errorBarStyle}</style></head>")

    withStyle.Replace("</body>", errorBarScript + "</body>")

  let private finish (theme: Theme) (height: string) (chart: GenericChart) =
    chart
    |> Chart.withLayout (layout theme)
    |> Chart.withXAxis xAxis
    |> Chart.withYAxis (yAxis theme)
    |> GenericChart.toEmbeddedHTML
    |> _.Replace("\"width\":600,", "")
    |> _.Replace("\"height\":600,", "")
    |> injectBodyStyle theme height

  // Trends-specific tuning for mobile:
  // - Compact margins maximise the narrow plot area.
  // - DragMode.False: touch is a tap (tooltip), not a scroll-hijacking pan.
  // - TickAngle=-45: rotated date labels don't overlap on narrow viewports.
  // - Horizontal centred legend avoids stealing horizontal width.
  // - DisplayModeBar=false: no floating toolbar (awkward on touch).
  // - ScrollZoom=NoZoom: wheel/pinch won't fight page scroll.
  let private trendsLayout (theme: Theme) =
    let bg = if theme = Dark then darkBg else transparent

    let font =
      if theme = Dark then
        Font.init (Color = darkFont)
      else
        Font.init ()

    let margin = Margin.init (Left = 48, Right = 16, Top = 24, Bottom = 56)

    Layout.init (
      PaperBGColor = bg,
      PlotBGColor = bg,
      Font = font,
      Margin = margin,
      DragMode = StyleParam.DragMode.False
    )

  let private trendsXAxis = LinearAxis.init (ShowGrid = false, TickAngle = -45)

  let private trendsConfig =
    Config.init (Responsive = true, DisplayModeBar = false, ScrollZoom = StyleParam.ScrollZoom.NoZoom)

  let private finishTrends (theme: Theme) (height: string) (chart: GenericChart) =
    chart
    |> Chart.withLayout (trendsLayout theme)
    |> Chart.withXAxis trendsXAxis
    |> Chart.withYAxis (yAxis theme)
    |> Chart.withConfig trendsConfig
    |> Chart.withLegendStyle (
      Orientation = StyleParam.Orientation.Horizontal,
      X = 0.5,
      XAnchor = StyleParam.XAnchorPosition.Center
    )
    |> GenericChart.toEmbeddedHTML
    |> _.Replace("\"width\":600,", "")
    |> _.Replace("\"height\":600,", "")
    |> injectBodyStyle theme height

  /// Classic x/y plot — one point per reading. Used by /history.
  let private renderIndividual (theme: Theme) (height: string) (readings: BloodPressureReading list) : string =
    let readings = readings |> List.sortBy _.Timestamp
    let timestamps = readings |> List.map (_.Timestamp >> Formats.formatLocal)
    let systolic = readings |> List.map _.Systolic
    let diastolic = readings |> List.map _.Diastolic
    let heartRate = readings |> List.map _.HeartRate
    let commented = readings |> List.filter _.Comments.IsSome

    let commentTraces =
      if commented.IsEmpty then
        []
      else
        let cTimestamps = commented |> List.map (_.Timestamp >> Formats.formatLocal)
        let cSystolic = commented |> List.map _.Systolic
        let cTexts = commented |> List.map (fun r -> r.Comments |> Option.defaultValue "")
        [ Chart.Point(x = cTimestamps, y = cSystolic, Name = "Comments", MultiText = cTexts) ]

    [ Chart.Line(x = timestamps, y = systolic, Name = "Systolic")
      |> Chart.withLineStyle (Color = systolicColor)
      Chart.Line(x = timestamps, y = diastolic, Name = "Diastolic")
      |> Chart.withLineStyle (Color = diastolicColor)
      Chart.Line(x = timestamps, y = heartRate, Name = "Heart Rate")
      yield! commentTraces ]
    |> Chart.combine
    |> Chart.withTitle "Blood Pressure History"
    |> finish theme height

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
  let private renderDashed
    (gran: Granularity)
    (theme: Theme)
    (height: string)
    (aggregated: AggregatedReading list)
    : string =
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
    |> finishTrends theme height

  let toHtml (theme: Theme) (height: string) = renderIndividual theme height
  let toHtmlDashed (gran: Granularity) (theme: Theme) (height: string) = renderDashed gran theme height
