namespace BpMonitor.Charts

open Plotly.NET
open BpMonitor.Core

module BpChart =
    let toHtml (readings: BloodPressureReading list) : string =
        let timestamps = readings |> List.map (fun r -> r.Timestamp.ToString("yyyy-MM-dd HH:mm"))
        let systolic   = readings |> List.map (fun r -> r.Systolic)
        let diastolic  = readings |> List.map (fun r -> r.Diastolic)
        let heartRate  = readings |> List.map (fun r -> r.HeartRate)

        [ Chart.Line(x = timestamps, y = systolic,  Name = "Systolic")
          Chart.Line(x = timestamps, y = diastolic, Name = "Diastolic")
          Chart.Line(x = timestamps, y = heartRate, Name = "Heart Rate") ]
        |> Chart.combine
        |> Chart.withTitle "Blood Pressure History"
        |> GenericChart.toEmbeddedHTML
