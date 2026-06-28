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

/// `now` for /recent's x-axis range — past the latest test reading (day 30), so every
/// fixture above falls inside the chart's initial 30-day window.
let private now = Timestamp.local 2026 2 1 12 0 0
let private windowStart10 = now.AddDays(-10.0)
let private windowStart30 = now.AddDays(-30.0)

/// True when the named trace's hover never repeats its own name — the legend already
/// shows it, color-coded. The negative lookahead stops the lazy match from crossing
/// into the next trace's "name" field, so this works regardless of which (if any)
/// fields a chart serializes between "name" and "hovertemplate".
let private hasNamelessHover (html: string) (name: string) =
  // After script-injection hardening, </extra> in the hover template is escaped to
  // <\/extra> (backslash + slash) in the raw HTML; match the backslash-escaped form.
  Regex.IsMatch(html, $"\"name\":\"{name}\"(?:(?!\"name\":).)*?\"hovertemplate\":\"[^\"]*<extra><\\\\/extra>\"")

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
let ``toHtml renders comment markers as a dark-red hexagon, matching Wegier et al. 2021 Fig. 5`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  let m = Regex.Match(html, "\"name\":\"Comments\".*?\"marker\":\\{([^}]*)\\}")
  test <@ m.Success @>
  let markerJson = m.Groups[1].Value
  // Plotly.NET serializes MarkerSymbol as its numeric Plotly code, not the name string —
  // "14" is hexagon (matches the "0"/"2" numeric codes for Circle/Diamond in the
  // toHtmlDashed snapshot).
  test <@ markerJson.Contains("\"symbol\":\"14\"") @>
  test <@ markerJson.Contains("\"color\":\"#8B0000\"") @>

[<Fact>]
let ``toHtml does not clip comment markers against the x-axis`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  let m = Regex.Match(html, "\"name\":\"Comments\".*?\"cliponaxis\":(true|false)")
  test <@ m.Success @>
  test <@ m.Groups[1].Value = "false" @>

[<Fact>]
let ``toHtml does not show "Comments" as a y-axis tick label even when comment markers are present`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ not (html.Contains("ticktext")) @>

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
let ``toHtml shows only the comment text on hover, not the "Comments" trace name`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  let m = Regex.Match(html, "\"name\":\"Comments\".*?\"hoverinfo\":\"([a-z]+)\"")
  test <@ m.Success @>
  test <@ m.Groups[1].Value = "text" @>

[<Fact>]
let ``toHtml does not include None comment readings in comments trace`` () =
  let noCommentOnly = [ reading 1 120 80 70 1 9 None ]
  let html = BpChart.toHtml GoalRange.defaults noCommentOnly
  test <@ not (html.Contains("Comments")) @>

[<Fact>]
let ``toHtml uses compact margins, like the trends chart, now that it has no title`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ html.Contains("\"margin\":{\"l\":48,\"r\":16,\"t\":24,\"b\":72}") @>

[<Fact>]
let ``toHtml renders a horizontal centered legend at the bottom, like the trends chart`` () =
  let html = BpChart.toHtml GoalRange.defaults readings

  test
    <@
      html.Contains(
        "\"legend\":{\"orientation\":\"h\",\"x\":0.5,\"xanchor\":\"center\",\"y\":-0.15,\"yanchor\":\"top\"}"
      )
    @>

[<Fact>]
let ``toHtml hover omits the redundant "Systolic"/"Diastolic" trace name, since the legend already shows it`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ hasNamelessHover html "Systolic" @>
  test <@ hasNamelessHover html "Diastolic" @>

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
  let html = BpChart.toHtmlRecent GoalRange.defaults 10 windowStart10 now sparse
  test <@ html.Contains("\"dash\":\"dash\"") @>

[<Fact>]
let ``toHtmlRecent connects readings with a solid line when the gap stays within 10% of the window`` () =
  // Window = 10 days; threshold = 1.0 missing day. Gap of 1 day → 0 missing days → solid.
  let dense = [ reading 1 120 80 70 1 9 None; reading 2 130 85 74 2 9 None ]
  let html = BpChart.toHtmlRecent GoalRange.defaults 10 windowStart10 now dense
  test <@ not (html.Contains("\"dash\":\"dash\"")) @>

[<Fact>]
let ``toHtmlRecent judges gaps by calendar days, not raw elapsed time`` () =
  // Window = 30 days; threshold = 3.0 missing days. Day 1 → Day 5 is a 4-calendar-day gap
  // (missingDays = 3, not > 3), so it must render solid even though the readings are
  // 9:00 → 10:00, i.e., raw elapsed time (4.0417 days) would cross the threshold if used directly.
  let readings = [ reading 1 120 80 70 1 9 None; reading 2 130 85 74 5 10 None ]
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings
  test <@ not (html.Contains("\"dash\":\"dash\"")) @>

[<Fact>]
let ``toHtmlRecent names each line trace after its series for the legend`` () =
  let html = BpChart.toHtmlRecent GoalRange.defaults 10 windowStart10 now readings

  let lineNameCount =
    Regex.Matches(html, "\"name\":\"(Systolic|Diastolic)\",\"showlegend\":[a-z]*,\"mode\":\"lines\\+markers\"").Count

  test <@ lineNameCount > 0 @>

[<Fact>]
let ``toHtmlRecent hover omits the redundant "Systolic"/"Diastolic" trace name, since the legend already shows it`` () =
  let html = BpChart.toHtmlRecent GoalRange.defaults 10 windowStart10 now readings
  test <@ hasNamelessHover html "Systolic" @>
  test <@ hasNamelessHover html "Diastolic" @>

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

  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings

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
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings

  test
    <@
      html.Contains(
        "\"legend\":{\"orientation\":\"h\",\"x\":0.5,\"xanchor\":\"center\",\"y\":-0.15,\"yanchor\":\"top\"}"
      )
    @>

[<Fact>]
let ``toHtmlRecent uses compact margins, like the trends chart, now that it has no title`` () =
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings
  test <@ html.Contains("\"margin\":{\"l\":48,\"r\":16,\"t\":24,\"b\":72}") @>

[<Fact>]
let ``toHtmlRecent shows exactly one legend entry per series, even when split across multiple dash/solid runs`` () =
  let readings =
    [ reading 1 120 80 70 1 9 None
      reading 2 121 80 70 2 9 None
      reading 3 122 80 70 3 9 None
      reading 4 123 80 70 10 9 None
      reading 5 124 80 70 11 9 None
      reading 6 125 80 70 12 9 None ]

  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings

  // "name" and "showlegend" are always serialized as adjacent fields, so matching them
  // directly avoids relying on which field happens to close the trace object last.
  let legendCount (name: string) =
    Regex.Matches(html, $"\"name\":\"{name}\",\"showlegend\":true").Count

  test <@ legendCount "Systolic" = 1 @>
  test <@ legendCount "Diastolic" = 1 @>

[<Fact>]
let ``toHtmlRecent renders a smoothed trend line for systolic and diastolic`` () =
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings
  test <@ html.Contains("\"name\":\"Systolic (trend)\"") @>
  test <@ html.Contains("\"name\":\"Diastolic (trend)\"") @>

[<Fact>]
let ``toHtmlRecent skips hover for the LOWESS trend trace, since its value is smoothed not measured`` () =
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings

  let hasHoverSkip (exactName: string) =
    Regex.IsMatch(html, $"\"name\":\"{Regex.Escape(exactName)}\".*?\"hoverinfo\":\"skip\"")

  test <@ hasHoverSkip "Systolic (trend)" @>
  test <@ hasHoverSkip "Diastolic (trend)" @>

[<Fact>]
let ``toHtmlRecent omits the trend line when there are too few readings to smooth meaningfully`` () =
  let sparse = [ reading 1 120 80 70 1 9 None; reading 2 130 85 74 2 9 None ]
  let html = BpChart.toHtmlRecent GoalRange.defaults 10 windowStart10 now sparse
  test <@ not (html.Contains("(trend)")) @>

[<Fact>]
let ``toHtmlRecent fades the raw measurement line so the LOWESS trend line stands out as the visual focus`` () =
  // Wegier et al. 2021, "Smoothing data": "The line graph of (raw) measurements was
  // then faded slightly to help the smoothing line stand out." — the raw series should
  // render at reduced opacity (rgba alpha < 1) while the trend series keeps full color.
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings

  let traceLineColor (exactName: string) =
    let m =
      Regex.Match(html, $"\"name\":\"{Regex.Escape(exactName)}\".*?\"line\":{{[^}}]*\"color\":\"([^\"]*)\"")

    test <@ m.Success @>
    m.Groups[1].Value

  test <@ (traceLineColor "Systolic").StartsWith("rgba") @>
  test <@ (traceLineColor "Diastolic").StartsWith("rgba") @>
  test <@ traceLineColor "Systolic (trend)" = "#008471" @>
  test <@ traceLineColor "Diastolic (trend)" = "#9C652B" @>

[<Fact>]
let ``toHtmlRecent shows a spikeline on the x-axis, to scrub through the chart and value strip`` () =
  // Wegier et al. 2021, "Scrubber bar": a vertical line tracks the cursor's horizontal
  // position across the display. Plotly's x-axis spikes give this for free.
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings
  test <@ html.Contains("\"showspikes\":true") @>

[<Fact>]
let ``toHtmlRecent opens focused on the last windowDays, even though all readings are loaded`` () =
  // The chart loads every reading (so panning left reveals older history), but its
  // initial x-axis range only spans [now-windowDays, now] rather than autoranging.
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings
  let rangeLow = Formats.formatLocal (now.AddDays(-30.0))
  let rangeHigh = Formats.formatLocal now
  test <@ html.Contains($"\"range\":[\"{rangeLow}\",\"{rangeHigh}\"]") @>

[<Fact>]
let ``toHtml (history) does not show a spikeline`` () =
  // The scrubber bar is a /recent-only affordance (it links to the value strip, which
  // only /recent has) — /history's chart keeps its plain x-axis.
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ not (html.Contains("\"showspikes\":true")) @>

[<Fact>]
let ``toHtml removes the lasso, autoscale and box-select modebar buttons, so the y-axis scale can't be visually distorted``
  ()
  =
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ html.Contains("\"modeBarButtonsToRemove\":[\"lasso2d\",\"autoScale2d\",\"select2d\"]") @>

[<Fact>]
let ``toHtml pre-selects the pan tool in the modebar, instead of defaulting to zoom-box drag`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  test <@ html.Contains("\"dragmode\":\"pan\"") @>

[<Fact>]
let ``toHtml locks the y-axis range so zoom/select tools can only ever change the x-axis`` () =
  let html = BpChart.toHtml GoalRange.defaults readings
  // The y-axis object nests a "title" object of its own, so a [^}]* lookahead would stop
  // at that inner brace; anchor on "range" (which precedes "fixedrange") instead.
  test <@ Regex.IsMatch(html, "\"yaxis\":\\{.*?\"range\":\\[0,200\\],\"fixedrange\":true") @>

[<Fact>]
let ``toHtmlRecent removes the lasso, autoscale and box-select modebar buttons, so the y-axis scale can't be visually distorted``
  ()
  =
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings
  test <@ html.Contains("\"modeBarButtonsToRemove\":[\"lasso2d\",\"autoScale2d\",\"select2d\"]") @>

[<Fact>]
let ``toHtmlRecent pre-selects the pan tool in the modebar, instead of defaulting to zoom-box drag`` () =
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings
  test <@ html.Contains("\"dragmode\":\"pan\"") @>

[<Fact>]
let ``toHtmlRecent locks the y-axis range so zoom/select tools can only ever change the x-axis`` () =
  let html = BpChart.toHtmlRecent GoalRange.defaults 30 windowStart30 now readings
  test <@ Regex.IsMatch(html, "\"yaxis\":\\{.*?\"range\":\\[0,200\\],\"fixedrange\":true") @>

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

[<Fact>]
let ``toHtmlDashed hover omits the redundant "Systolic"/"Diastolic" trace name, since the legend already shows it`` () =
  let html = BpChart.toHtmlDashed GoalRange.defaults Weekly (asAggregated readings)
  test <@ hasNamelessHover html "Systolic" @>
  test <@ hasNamelessHover html "Diastolic" @>

[<Fact>]
let ``toHtmlDashed Monthly: x-axis labels use ISO week format`` () =
  // Jan 8, 2026 is a Thursday in ISO week 2
  let aggregated = asAggregated [ reading 1 120 80 70 8 9 None ]
  let html = BpChart.toHtmlDashed GoalRange.defaults Monthly aggregated
  test <@ html.Contains("W2") @>

[<Fact>]
let ``toHtmlDashed Yearly: x-axis labels use month-name format`` () =
  // Jan 8, 2026 → "Jan"
  let aggregated = asAggregated [ reading 1 120 80 70 8 9 None ]
  let html = BpChart.toHtmlDashed GoalRange.defaults Yearly aggregated
  test <@ html.Contains("Jan") @>

[<Fact>]
let ``toHtml escapes </script> in comment text to prevent inline script injection`` () =
  let hostile = reading 1 120 80 70 1 9 (Some "</script><img src=x onerror=alert(1)>")
  let html = BpChart.toHtml GoalRange.defaults [ hostile ]

  // The raw injection string must not appear — it would close the <script> block early
  // and allow HTML to be injected into the page DOM
  test <@ not (html.Contains "</script><img") @>
  // The backslash-escaped form must appear in the JSON data instead
  // (<\/ is valid JSON and is treated as </ by JS but the HTML parser won't see it as </script>)
  test <@ html.Contains @"<\/script><img" @>
  // The actual </script> closing tags must still be present (the page must remain valid)
  test <@ html.Contains "</script>" @>
