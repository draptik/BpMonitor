namespace BpMonitor.Charts

open Plotly.NET
open BpMonitor.Core

module BpChart =
  let private darkLayout =
    Layout.init (
      PaperBGColor = Color.fromString "#11191f",
      PlotBGColor = Color.fromString "#11191f",
      Font = Font.init (Color = Color.fromString "#c2cfd6")
    )

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
    |> (if theme = "dark" then Chart.withLayout darkLayout else id)
    |> GenericChart.toEmbeddedHTML
    |> _.Replace("\"width\":600,", "")

  let toHtml (theme: string) =
    render StyleParam.DrawingStyle.Solid theme

  let toHtmlDashed (theme: string) =
    render StyleParam.DrawingStyle.Dash theme
