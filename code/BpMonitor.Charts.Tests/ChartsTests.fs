module ChartsTests

open System
open System.Text.RegularExpressions
open System.Threading.Tasks
open BpMonitor.TestSupport
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Charts

let private thisFile = IO.Path.Combine(__SOURCE_DIRECTORY__, __SOURCE_FILE__)
let private verifyHtml = Verifier.verifyHtml thisFile

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
let ``toHtml renders a goal-range band shaped rectangle for systolic and diastolic bounds`` () =
  let goal: GoalRange =
    { SystolicMin = 90
      SystolicMax = 140
      DiastolicMin = 60
      DiastolicMax = 90 }

  let html = BpChart.toHtml goal readings
  test <@ html.Contains("\"type\":\"rect\"") @>
  test <@ html.Contains("\"y0\":90") @>
  test <@ html.Contains("\"y1\":140") @>
  test <@ html.Contains("\"y0\":60") @>
  test <@ html.Contains("\"y1\":90") @>

[<Fact>]
let ``toHtml sets a denser y-axis tick interval than plotly's default`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ html.Contains("\"dtick\":20") @>

[<Fact>]
let ``toHtml renders a visible y-axis line with a theme-neutral default color`` () =
  // "#444" is the same neutral gray used as the light-theme font color in theme.js;
  // the client overrides it per-theme on load via Plotly.relayout, so the server-rendered
  // default only needs to be readable, not theme-aware.
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ html.Contains("\"showline\":true") @>
  test <@ html.Contains("\"linecolor\":\"#444\"") @>

[<Fact>]
let ``toHtml renders a visible x-axis line`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ Regex.IsMatch(html, "\"xaxis\":\\{[^}]*\"showline\":true") @>

[<Fact>]
let ``toHtml labels the y-axis with blood pressure and the mmHg unit`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ html.Contains("\"title\":{\"text\":\"blood pressure [mmHg]\"}") @>

[<Fact>]
let ``toHtml renders tick marks on both axes`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  let tickCount = Regex.Matches(html, "\"ticks\":\"outside\"").Count
  test <@ tickCount = 2 @>

[<Fact>]
let ``toHtml gives tick marks the same theme-neutral color as the axis line, so they stay visible in dark mode`` () =
  // Axis lines already use "#444" and get relayouted per-theme by theme.js; ticks need the
  // same starting color, otherwise they keep Plotly's near-invisible-on-dark default.
  let html = BpChart.toHtml GoalRange.defaults readings
  let tickColorCount = Regex.Matches(html, "\"tickcolor\":\"#444\"").Count
  test <@ tickColorCount = 2 @>

[<Fact>]
let ``toHtml renders a marker at every systolic and diastolic data point`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  let modeCount = Regex.Matches(html, "\"mode\":\"lines\\+markers\"").Count
  test <@ modeCount = 2 @> // Systolic + Diastolic; heart rate is never included via toHtml

[<Fact>]
let ``toHtml plots comment markers on the x-axis baseline (y=0), not at the reading's value`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  let m = Regex.Match(html, "\"name\":\"Comments\".*?\"y\":\\[([^\\]]*)\\]")
  test <@ m.Success @>
  let yValues = m.Groups[1].Value.Split(',') |> Array.map int
  test <@ yValues.Length > 0 @>
  test <@ yValues |> Array.forall (fun y -> y = 0) @>

[<Fact>]
let ``toHtml renders timestamps in ascending order regardless of input order`` () =
  let reversed = List.rev readings
  let html = BpChart.toHtml GoalRange.defaults reversed
  let pos1 = html.IndexOf("2026-01-01")
  let pos30 = html.IndexOf("2026-01-30")
  test <@ pos1 < pos30 @>

[<Fact>]
let ``toHtml includes comment text as hover info for commented readings`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ html.Contains("After coffee") @>
  test <@ html.Contains("Stressful day") @>
  test <@ html.Contains("After walk") @>
  test <@ html.Contains("Work deadline") @>

[<Fact>]
let ``toHtml does not include None comment readings in comments trace`` () =
  let noCommentOnly = [ reading 1 120 80 70 1 9 None ]
  let html = BpChart.toHtml GoalRange.defaults noCommentOnly
  test <@ not (html.Contains("Comments")) @>

[<Fact>]
let ``toHtml uses compact margins, like the trends chart, now that it has no title`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ html.Contains("\"margin\":{\"l\":48,\"r\":16,\"t\":24,\"b\":56}") @>

[<Fact>]
let ``toHtml matches snapshot`` () : Task =
  let html: string = BpChart.toHtml GoalRange.defaults readings
  verifyHtml html

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
let ``toHtmlRecent renders a dashed line segment when a gap exceeds 10% of the window as missing days`` () =
  // Window = 10 days; threshold = 1.0 missing day. Gap of 6 days → 5 missing days → dashed.
  let sparse = [ reading 1 120 80 70 1 9 None; reading 2 130 85 74 7 9 None ]
  let html = BpChart.toHtmlRecent GoalRange.defaults 10 sparse
  test <@ html.Contains("\"dash\":\"dash\"") @>

[<Fact>]
let ``toHtmlRecent connects readings with a solid line when the gap stays within 10% of the window`` () =
  // Window = 10 days; threshold = 1.0 missing day. Gap of 1 day → 0 missing days → solid.
  let dense = [ reading 1 120 80 70 1 9 None; reading 2 130 85 74 2 9 None ]
  let html = BpChart.toHtmlRecent GoalRange.defaults 10 dense
  test <@ not (html.Contains("\"dash\":\"dash\"")) @>

[<Fact>]
let ``toHtmlRecent judges gaps by calendar days, not raw elapsed time`` () =
  // Window = 30 days; threshold = 3.0 missing days. Day 1 → Day 5 is a 4-calendar-day gap
  // (missingDays = 3, not > 3), so it must render solid even though the readings are
  // 9:00 → 10:00, i.e. raw elapsed time (4.0417 days) would cross the threshold if used directly.
  let readings = [ reading 1 120 80 70 1 9 None; reading 2 130 85 74 5 10 None ]
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 readings
  test <@ not (html.Contains("\"dash\":\"dash\"")) @>

[<Fact>]
let ``toHtmlRecent names each line trace after its series for hover text`` () =
  let html = BpChart.toHtmlRecent GoalRange.defaults 10 readings

  let lineNameCount =
    Regex.Matches(html, "\"name\":\"(Systolic|Diastolic)\",\"showlegend\":[a-z]*,\"mode\":\"lines\\+markers\"").Count

  test <@ lineNameCount > 0 @>

[<Fact>]
let ``toHtmlRecent does not drop any reading, even when split across dash/solid runs`` () =
  // Window = 30 days; threshold = 3 missing days. Days 1-2-3 (solid), a 7-day gap to day 10
  // (6 missing days, dashed), then days 10-11-12 (solid) — 3 runs sharing 2 boundary points.
  let readings =
    [ reading 1 120 80 70 1 9 None
      reading 2 121 80 70 2 9 None
      reading 3 122 80 70 3 9 None
      reading 4 123 80 70 10 9 None
      reading 5 124 80 70 11 9 None
      reading 6 125 80 70 12 9 None ]

  let html = BpChart.toHtmlRecent GoalRange.defaults 30 readings

  let coveredLabels (name: string) =
    Regex.Matches(html, $"\"name\":\"{name}\".*?\"x\":\\[([^\\]]*)\\]")
    |> Seq.collect (fun m -> m.Groups[1].Value.Split(','))
    |> Set.ofSeq

  test <@ (coveredLabels "Systolic").Count = readings.Length @>
  test <@ (coveredLabels "Diastolic").Count = readings.Length @>
  test <@ html.Contains("\"dash\":\"dash\"") @>
  test <@ html.Contains("\"dash\":\"solid\"") @>

[<Fact>]
let ``toHtmlRecent renders a horizontal centered legend at the bottom, like the trends chart`` () =
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 readings
  test <@ html.Contains("\"legend\":{\"orientation\":\"h\",\"x\":0.5,\"xanchor\":\"center\"}") @>

[<Fact>]
let ``toHtmlRecent uses compact margins, like the trends chart, now that it has no title`` () =
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 readings
  test <@ html.Contains("\"margin\":{\"l\":48,\"r\":16,\"t\":24,\"b\":56}") @>

[<Fact>]
let ``toHtmlRecent shows exactly one legend entry per series, even when split across multiple dash/solid runs`` () =
  let readings =
    [ reading 1 120 80 70 1 9 None
      reading 2 121 80 70 2 9 None
      reading 3 122 80 70 3 9 None
      reading 4 123 80 70 10 9 None
      reading 5 124 80 70 11 9 None
      reading 6 125 80 70 12 9 None ]

  let html = BpChart.toHtmlRecent GoalRange.defaults 30 readings

  // Each trace object ends at the first "}}" (closing its "line" object then itself), so
  // bounding the match there keeps "name"/"showlegend" from leaking into the next trace.
  let legendCount (name: string) =
    Regex.Matches(html, $"\"name\":\"{name}\".*?}}}}")
    |> Seq.filter (fun m -> m.Value.Contains("\"showlegend\":true"))
    |> Seq.length

  test <@ legendCount "Systolic" = 1 @>
  test <@ legendCount "Diastolic" = 1 @>

[<Fact>]
let ``toHtmlDashed matches snapshot`` () : Task =
  let html: string =
    BpChart.toHtmlDashed GoalRange.defaults Weekly (asAggregated readings)

  verifyHtml html

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

  let html = BpChart.toHtmlDashed GoalRange.defaults Weekly aggregated
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

  let html = BpChart.toHtmlDashed GoalRange.defaults Weekly aggregated
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

  let html = BpChart.toHtmlDashed GoalRange.defaults Weekly aggregated
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

  let html = BpChart.toHtmlDashed GoalRange.defaults Weekly aggregated
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

  let html = BpChart.toHtmlDashed GoalRange.defaults Weekly aggregated
  // Systolic trace hover: "2 readings · 110–130"
  test <@ html.Contains("2 readings") @>
  test <@ html.Contains("110") @>
  test <@ html.Contains("130") @>
