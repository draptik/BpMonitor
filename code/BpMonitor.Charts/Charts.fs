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

  let private render
    (lineDash: StyleParam.DrawingStyle)
    (theme: string)
    (readings: BloodPressureReading list)
    : string =
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

    let showMarkers = lineDash <> StyleParam.DrawingStyle.Solid

    [ Chart.Line(x = timestamps, y = systolic, Name = "Systolic", LineDash = lineDash, ShowMarkers = showMarkers)
      Chart.Line(x = timestamps, y = diastolic, Name = "Diastolic", LineDash = lineDash, ShowMarkers = showMarkers)
      Chart.Line(x = timestamps, y = heartRate, Name = "Heart Rate", LineDash = lineDash, ShowMarkers = showMarkers)
      yield! commentTraces ]
    |> Chart.combine
    |> Chart.withTitle "Blood Pressure History"
    |> Chart.withLayout (layout theme)
    |> Chart.withXAxis xAxis
    |> Chart.withYAxis (yAxis theme)
    |> GenericChart.toEmbeddedHTML
    |> _.Replace("\"width\":600,", "")
    |> injectBodyStyle theme

  let toHtml (theme: string) =
    render StyleParam.DrawingStyle.Solid theme

  let toHtmlDashed (theme: string) =
    render StyleParam.DrawingStyle.Dash theme
