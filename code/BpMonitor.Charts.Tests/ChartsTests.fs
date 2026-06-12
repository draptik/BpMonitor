module ChartsTests

open System
open System.Text.RegularExpressions
open System.Threading.Tasks
open Xunit
open Swensen.Unquote
open VerifyXunit
open BpMonitor.Core
open BpMonitor.Charts


let private reading id systolic diastolic heartRate day hour comment =
  { Id = id
    MemberId = 1
    Systolic = systolic
    Diastolic = diastolic
    HeartRate = heartRate
    Timestamp = Timestamp.local 2026 1 day hour 0 0
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

[<Fact>]
let ``toHtml renders timestamps in ascending order regardless of input order`` () =
  let reversed = List.rev readings
  let html = BpChart.toHtml Light reversed
  let pos1 = html.IndexOf("2026-01-01")
  let pos30 = html.IndexOf("2026-01-30")
  test <@ pos1 < pos30 @>

[<Fact>]
let ``toHtml includes comment text as hover info for commented readings`` () =
  let html = BpChart.toHtml Light readings
  test <@ html.Contains("After coffee") @>
  test <@ html.Contains("Stressful day") @>
  test <@ html.Contains("After walk") @>
  test <@ html.Contains("Work deadline") @>

[<Fact>]
let ``toHtml does not include None comment readings in comments trace`` () =
  let noCommentOnly = [ reading 1 120 80 70 1 9 None ]
  let html = BpChart.toHtml Light noCommentOnly
  test <@ not (html.Contains("Comments")) @>

[<Fact>]
let ``toHtml dark theme output contains dark background color`` () =
  let html = BpChart.toHtml Dark readings
  test <@ html.Contains("#11191f") @>

[<Fact>]
let ``toHtml matches snapshot`` () : Task =
  let html: string = BpChart.toHtml Light readings
  let settings = VerifyTests.VerifySettings()
  settings.ScrubInlineGuids()

  settings.AddScrubber(fun sb ->
    let scrubbed =
      Regex.Replace(string sb, @"renderPlotly_[0-9a-f]{32}", "renderPlotly_GUID")

    sb.Clear().Append(scrubbed) |> ignore)

  Verifier.Verify(html, settings).ToTask()

/// Wrap a list of readings as single-reading aggregated points (Count = 1 each).
let private asAggregated (rs: BloodPressureReading list) =
  rs
  |> List.map (fun r ->
    { Reading = r
      Count = 1
      MinSystolic = r.Systolic
      MaxSystolic = r.Systolic
      MinDiastolic = r.Diastolic
      MaxDiastolic = r.Diastolic })

[<Fact>]
let ``toHtmlDashed matches snapshot`` () : Task =
  let html: string = BpChart.toHtmlDashed Weekly Light (asAggregated readings)
  let settings = VerifyTests.VerifySettings()
  settings.ScrubInlineGuids()

  settings.AddScrubber(fun sb ->
    let scrubbed =
      Regex.Replace(string sb, @"renderPlotly_[0-9a-f]{32}", "renderPlotly_GUID")

    sb.Clear().Append(scrubbed) |> ignore)

  Verifier.Verify(html, settings).ToTask()

[<Fact>]
let ``toHtmlDashed: multi-reading period uses diamond marker (size 11) and 'readings (avg)' hover`` () =
  // Count = 2 → larger diamond marker (size 11, Plotly symbol "2") + hover "2 readings (avg)"
  let aggregated =
    [ { Reading = readings[0]
        Count = 2
        MinSystolic = readings[0].Systolic - 10
        MaxSystolic = readings[0].Systolic + 10
        MinDiastolic = readings[0].Diastolic - 5
        MaxDiastolic = readings[0].Diastolic + 5 } ]

  let html = BpChart.toHtmlDashed Weekly Light aggregated
  test <@ html.Contains("readings") @>
  test <@ html.Contains("\"size\":[11]") @> // diamond is rendered larger than circle
  test <@ html.Contains("\"symbol\":[\"2\"]") @> // Plotly numeric code for Diamond

[<Fact>]
let ``toHtmlDashed: single-reading period uses circle marker (size 8) and '1 reading' hover`` () =
  // Count = 1 → standard circle marker (size 8, Plotly symbol "0") + hover "1 reading"
  let aggregated =
    [ { Reading = readings[0]
        Count = 1
        MinSystolic = readings[0].Systolic
        MaxSystolic = readings[0].Systolic
        MinDiastolic = readings[0].Diastolic
        MaxDiastolic = readings[0].Diastolic } ]

  let html = BpChart.toHtmlDashed Weekly Light aggregated
  test <@ html.Contains("1 reading") @>
  test <@ html.Contains("\"size\":[8]") @> // circle is smaller than diamond
  test <@ html.Contains("\"symbol\":[\"0\"]") @> // Plotly numeric code for Circle

[<Fact>]
let ``toHtmlDashed: multi-reading period renders error_y with non-zero spread`` () =
  // avg sys=120, min=110, max=135 → upper offset=15, lower offset=10
  let aggregated =
    [ { Reading = readings[0] // Systolic=120, Diastolic=80
        Count = 3
        MinSystolic = 110
        MaxSystolic = 135
        MinDiastolic = 75
        MaxDiastolic = 90 } ]

  let html = BpChart.toHtmlDashed Weekly Light aggregated
  test <@ html.Contains("\"error_y\"") @>
  test <@ html.Contains("\"type\":\"data\"") @>
  test <@ html.Contains("\"symmetric\":false") @>

[<Fact>]
let ``toHtmlDashed: single-reading period has zero-spread error_y`` () =
  // min = max = avg → upper and lower offsets are both 0
  let aggregated =
    [ { Reading = readings[0] // Systolic=120, Diastolic=80
        Count = 1
        MinSystolic = readings[0].Systolic
        MaxSystolic = readings[0].Systolic
        MinDiastolic = readings[0].Diastolic
        MaxDiastolic = readings[0].Diastolic } ]

  let html = BpChart.toHtmlDashed Weekly Light aggregated
  // error_y present but array values are all 0
  test <@ html.Contains("\"error_y\"") @>
  test <@ html.Contains("\"array\":[0]") @>
  test <@ html.Contains("\"arrayminus\":[0]") @>

[<Fact>]
let ``toHtmlDashed: multi-reading systolic tooltip shows count and range`` () =
  let aggregated =
    [ { Reading = readings[0] // Systolic=120
        Count = 2
        MinSystolic = 110
        MaxSystolic = 130
        MinDiastolic = 75
        MaxDiastolic = 85 } ]

  let html = BpChart.toHtmlDashed Weekly Light aggregated
  // Systolic trace hover: "2 readings · 110–130"
  test <@ html.Contains("2 readings") @>
  test <@ html.Contains("110") @>
  test <@ html.Contains("130") @>
