namespace BpMonitor.Charts

open System.Globalization
open Plotly.NET
open Plotly.NET.LayoutObjects
open BpMonitor.Core

module BpChart =
  // ── palette ──────────────────────────────────────────────────────────────
  let private darkBgHex = "#11191f"
  let private darkBg = Color.fromString darkBgHex
  let private transparent = Color.fromString "rgba(0,0,0,0)"
  let private darkFont = Color.fromString "#c2cfd6"
  let private darkGridLine = Color.fromString "rgba(194,207,214,0.12)"
  let private lightGridLine = Color.fromString "rgba(0,0,0,0.08)"
  let private systolicColor = Color.fromString "rgba(99,110,250,1)"

  let private layout (theme: string) =
    let bg = if theme = "dark" then darkBg else transparent

    let font =
      if theme = "dark" then
        Font.init (Color = darkFont)
      else
        Font.init ()

    Layout.init (PaperBGColor = bg, PlotBGColor = bg, Font = font)

  let private xAxis = LinearAxis.init (ShowGrid = false)

  let private yAxis (theme: string) =
    let gridColor = if theme = "dark" then darkGridLine else lightGridLine
    LinearAxis.init (GridColor = gridColor)

  // The chart is rendered inside an iframe (separate document), so the iframe body
  // background must be set explicitly — it does not inherit the parent page theme.
  let private injectBodyStyle (theme: string) (html: string) =
    if theme = "dark" then
      html.Replace("</head>", $"<style>body{{background:{darkBgHex};margin:0}}</style></head>")
    else
      html.Replace("</head>", "<style>body{margin:0}</style></head>")

  let private finish (theme: string) (chart: GenericChart) =
    chart
    |> Chart.withLayout (layout theme)
    |> Chart.withXAxis xAxis
    |> Chart.withYAxis (yAxis theme)
    |> GenericChart.toEmbeddedHTML
    |> _.Replace("\"width\":600,", "")
    |> injectBodyStyle theme

  // Trends-specific tuning for mobile:
  // - Compact margins maximise the narrow plot area.
  // - DragMode.False: touch is a tap (tooltip), not a scroll-hijacking pan.
  // - TickAngle=-45: rotated date labels don't overlap on narrow viewports.
  // - Horizontal centred legend avoids stealing horizontal width.
  // - DisplayModeBar=false: no floating toolbar (awkward on touch).
  // - ScrollZoom=NoZoom: wheel/pinch won't fight page scroll.
  let private trendsLayout (theme: string) =
    let bg = if theme = "dark" then darkBg else transparent

    let font =
      if theme = "dark" then
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

  let private finishTrends (theme: string) (chart: GenericChart) =
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
    |> injectBodyStyle theme

  /// Classic x/y plot — one point per reading. Used by /history.
  let private renderIndividual (theme: string) (readings: BloodPressureReading list) : string =
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
      Chart.Line(x = timestamps, y = diastolic, Name = "Diastolic")
      Chart.Line(x = timestamps, y = heartRate, Name = "Heart Rate")
      yield! commentTraces ]
    |> Chart.combine
    |> Chart.withTitle "Blood Pressure History"
    |> finish theme

  type private DailyPoint =
    { Label: string
      Systolic: int
      Diastolic: int
      Symbol: StyleParam.MarkerSymbol
      Size: int
      HoverText: string }

  /// Dashed line chart — one point per reading, connected by a dashed line with circle markers.
  /// Readings should be pre-aggregated by the caller (daily / weekly / monthly averages).
  /// X-axis labels adapt to granularity: Weekly → date, Monthly → ISO week, Yearly → month name.
  /// Used by /trends for all granularities.
  let private renderDashed (gran: Granularity) (theme: string) (readings: BloodPressureReading list) : string =
    let readings = readings |> List.sortBy _.Timestamp

    let xLabel (r: BloodPressureReading) =
      let local = r.Timestamp.ToLocalTime()

      match gran with
      | Weekly -> local.Date.ToString("d MMM")
      | Monthly ->
        let week = ISOWeek.GetWeekOfYear(local.Date)
        $"W{week}"
      | Yearly -> local.Date.ToString("MMM")

    let dailyPoints =
      readings
      |> List.map (fun r ->
        { Label = xLabel r
          Systolic = r.Systolic
          Diastolic = r.Diastolic
          Symbol = StyleParam.MarkerSymbol.Circle
          Size = 8
          HoverText = "" })

    let timestamps = dailyPoints |> List.map _.Label
    let systolic = dailyPoints |> List.map _.Systolic
    let diastolic = dailyPoints |> List.map _.Diastolic
    let symbols = dailyPoints |> List.map _.Symbol
    let sizes = dailyPoints |> List.map _.Size
    let hoverTexts = dailyPoints |> List.map _.HoverText

    let commented = readings |> List.filter _.Comments.IsSome

    let commentTraces =
      if commented.IsEmpty then
        []
      else
        let cTimestamps = commented |> List.map xLabel

        let cSystolic = commented |> List.map _.Systolic
        let cTexts = commented |> List.map (fun r -> r.Comments |> Option.defaultValue "")

        [ Chart.Point(
            x = cTimestamps,
            y = cSystolic,
            Name = "Comments",
            MultiText = cTexts,
            Opacity = 0.5,
            MarkerColor = systolicColor
          ) ]

    let line name y text =
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
      |> Chart.withMarkerStyle (MultiSize = sizes)

    // Reading count only on Systolic — showing it on every trace would multiply the count in hover.
    [ line "Systolic" systolic hoverTexts
      line "Diastolic" diastolic (List.replicate diastolic.Length "")
      yield! commentTraces ]
    |> Chart.combine
    |> finishTrends theme

  let toHtml (theme: string) = renderIndividual theme
  let toHtmlDashed (gran: Granularity) (theme: string) = renderDashed gran theme
