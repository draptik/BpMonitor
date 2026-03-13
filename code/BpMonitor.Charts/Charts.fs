namespace BpMonitor.Charts

open Plotly.NET
open BpMonitor.Core

module BpChart =
  let toHtml (readings: BloodPressureReading list) : string =
    let readings = readings |> List.sortBy _.Timestamp
    let timestamps = readings |> List.map _.Timestamp.ToString("yyyy-MM-dd HH:mm")
    let systolic = readings |> List.map _.Systolic
    let diastolic = readings |> List.map _.Diastolic
    let heartRate = readings |> List.map _.HeartRate

    [ Chart.Line(x = timestamps, y = systolic, Name = "Systolic")
      Chart.Line(x = timestamps, y = diastolic, Name = "Diastolic")
      Chart.Line(x = timestamps, y = heartRate, Name = "Heart Rate") ]
    |> Chart.combine
    |> Chart.withTitle "Blood Pressure History"
    |> GenericChart.toEmbeddedHTML
    |> fun html -> html.Replace("\"width\":600,", "")
