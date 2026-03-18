namespace BpMonitor.Charts

open Plotly.NET
open BpMonitor.Core

module BpChart =
  let toHtml (readings: BloodPressureReading list) : string =
    let readings = readings |> List.sortBy _.Timestamp

    let timestamps = readings |> List.map _.Timestamp.ToString(Formats.timestamp)

    let systolic = readings |> List.map _.Systolic
    let diastolic = readings |> List.map _.Diastolic
    let heartRate = readings |> List.map _.HeartRate

    let commented = readings |> List.filter _.Comments.IsSome

    let commentTraces =
      if commented.IsEmpty then
        []
      else
        let cTimestamps = commented |> List.map _.Timestamp.ToString(Formats.timestamp)
        let cSystolic = commented |> List.map _.Systolic
        let cTexts = commented |> List.map (fun r -> r.Comments |> Option.defaultValue "")

        [ Chart.Point(x = cTimestamps, y = cSystolic, Name = "Comments", MultiText = cTexts) ]

    [ Chart.Line(x = timestamps, y = systolic, Name = "Systolic")
      Chart.Line(x = timestamps, y = diastolic, Name = "Diastolic")
      Chart.Line(x = timestamps, y = heartRate, Name = "Heart Rate")
      yield! commentTraces ]
    |> Chart.combine
    |> Chart.withTitle "Blood Pressure History"
    |> GenericChart.toEmbeddedHTML
    |> _.Replace("\"width\":600,", "")
