namespace BpMonitor.Charts

open Plotly.NET
open Plotly.NET.LayoutObjects
open BpMonitor.Core

module BpChart =
  let private transparent = Color.fromString "rgba(0,0,0,0)"
  let private darkBg = Color.fromString "#11191f"

  let private layout (theme: string) =
    let bg = if theme = "dark" then darkBg else transparent

    let font =
      if theme = "dark" then
        Font.init (Color = Color.fromString "#c2cfd6")
      else
        Font.init ()

    Layout.init (PaperBGColor = bg, PlotBGColor = bg, Font = font)

  let private xAxis = LinearAxis.init (ShowGrid = false)

  let private yAxis (theme: string) =
    let gridColor =
      if theme = "dark" then
        Color.fromString "rgba(194,207,214,0.12)"
      else
        Color.fromString "rgba(0,0,0,0.08)"

    LinearAxis.init (GridColor = gridColor)

  // The chart is rendered inside an iframe (separate document), so the iframe body
  // background must be set explicitly — it does not inherit the parent page theme.
  let private injectBodyStyle (theme: string) (html: string) =
    if theme = "dark" then
      html.Replace("</head>", "<style>body{background:#11191f;margin:0}</style></head>")
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

  /// Daily-grouped plot — one point per calendar day, averaged when multiple readings exist.
  /// Circle = single reading, Diamond = daily average. Used by /trends windowed chart.
  let private renderDailyGrouped (theme: string) (readings: BloodPressureReading list) : string =
    let readings = readings |> List.sortBy _.Timestamp

    let dailyPoints =
      readings
      |> List.groupBy (fun r -> r.Timestamp.LocalDateTime.Date)
      |> List.sortBy fst
      |> List.map (fun (date, dayReadings) ->
        let count = dayReadings.Length

        let avg f =
          dayReadings |> List.averageBy (fun r -> float (f r)) |> int

        let label = date.ToString("yyyy-MM-dd")

        let symbol =
          if count = 1 then
            StyleParam.MarkerSymbol.Circle
          else
            StyleParam.MarkerSymbol.Diamond

        let hoverText =
          if count = 1 then
            "1 reading"
          else
            $"{count} readings (daily avg)"

        label, avg _.Systolic, avg _.Diastolic, avg _.HeartRate, symbol, hoverText)

    let timestamps = dailyPoints |> List.map (fun (t, _, _, _, _, _) -> t)
    let systolic = dailyPoints |> List.map (fun (_, s, _, _, _, _) -> s)
    let diastolic = dailyPoints |> List.map (fun (_, _, d, _, _, _) -> d)
    let symbols = dailyPoints |> List.map (fun (_, _, _, _, sym, _) -> sym)
    let hoverTexts = dailyPoints |> List.map (fun (_, _, _, _, _, h) -> h)

    let commented = readings |> List.filter _.Comments.IsSome

    let commentTraces =
      if commented.IsEmpty then
        []
      else
        let cTimestamps =
          commented
          |> List.map (fun r -> r.Timestamp.LocalDateTime.Date.ToString("yyyy-MM-dd"))

        let cSystolic = commented |> List.map _.Systolic
        let cTexts = commented |> List.map (fun r -> r.Comments |> Option.defaultValue "")
        [ Chart.Point(x = cTimestamps, y = cSystolic, Name = "Comments", MultiText = cTexts, Opacity = 0.5) ]

    let line name y =
      Chart.Line(
        x = timestamps,
        y = y,
        Name = name,
        LineDash = StyleParam.DrawingStyle.Dash,
        ShowMarkers = true,
        MultiMarkerSymbol = symbols,
        MultiText = hoverTexts,
        LineWidth = 1.0
      )
      |> Chart.withMarkerStyle (Size = 8)

    [ line "Systolic" systolic; line "Diastolic" diastolic; yield! commentTraces ]
    |> Chart.combine
    |> Chart.withTitle "Blood Pressure History"
    |> finish theme

  let toHtml (theme: string) = renderIndividual theme
  let toHtmlDashed (theme: string) = renderDailyGrouped theme
