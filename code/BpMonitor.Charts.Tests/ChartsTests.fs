module ChartsTests

open System
open System.Text.RegularExpressions
open System.Threading.Tasks
open Xunit
open VerifyXunit
open BpMonitor.Core
open BpMonitor.Charts


let private reading id systolic diastolic heartRate day hour comment =
  { Id = id
    Systolic = systolic
    Diastolic = diastolic
    HeartRate = heartRate
    Timestamp = DateTimeOffset(2026, 1, day, hour, 0, 0, TimeSpan.Zero)
    Comments = comment
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let private readings =
  [ reading 1 120 80 70 1 9 None
    reading 2 135 88 78 2 8 (Some "After coffee")
    reading 3 118 76 65 3 10 None
    reading 4 142 92 82 4 7 (Some "Stressful day")
    reading 5 125 83 72 5 9 None
    reading 6 130 85 74 6 8 None
    reading 7 115 75 68 7 11 (Some "After walk")
    reading 8 128 84 73 8 9 None
    reading 9 138 90 80 9 8 None
    reading 10 122 81 71 10 9 None
    reading 11 117 78 67 11 7 (Some "Good sleep")
    reading 12 132 87 76 12 9 None
    reading 13 145 95 85 13 8 (Some "No sleep")
    reading 14 119 79 69 14 10 None
    reading 15 126 82 72 15 9 None
    reading 16 133 88 77 16 8 None
    reading 17 121 80 70 17 9 (Some "Relaxed")
    reading 18 129 85 74 18 7 None
    reading 19 140 91 81 19 9 None
    reading 20 116 76 66 20 10 (Some "After gym")
    reading 21 124 82 71 21 9 None
    reading 22 131 86 75 22 8 None
    reading 23 118 77 68 23 9 None
    reading 24 136 89 79 24 7 (Some "Late night")
    reading 25 123 81 72 25 9 None
    reading 26 127 83 73 26 10 None
    reading 27 143 93 83 27 8 (Some "Work deadline")
    reading 28 119 78 69 28 9 None
    reading 29 122 80 71 29 9 None
    reading 30 128 84 74 30 8 None ]

type ChartTests() =
  [<Fact>]
  member _.``toHtml renders timestamps in ascending order regardless of input order``() =
    let reversed = List.rev readings
    let html = BpChart.toHtml reversed
    let pos1 = html.IndexOf("2026-01-01")
    let pos30 = html.IndexOf("2026-01-30")
    Assert.True(pos1 < pos30, "First timestamp should appear before last in chart data")

  [<Fact>]
  member _.``toHtml matches snapshot``() : Task =
    let html = BpChart.toHtml readings
    let settings = VerifyTests.VerifySettings()
    settings.ScrubInlineGuids()

    settings.AddScrubber(fun sb ->
      let scrubbed =
        Regex.Replace(string sb, @"renderPlotly_[0-9a-f]{32}", "renderPlotly_GUID")

      sb.Clear().Append(scrubbed) |> ignore)

    Verifier.Verify(html, settings).ToTask()
