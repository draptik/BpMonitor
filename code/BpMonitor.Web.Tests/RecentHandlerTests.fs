module RecentHandlerTests

open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

let private now = Timestamp.utc 2026 6 17 12 0 0

// Marge Simpson's readings for the last 5 years anchored to `now`, stamped as member 1.
// Deterministic (fixed seed 1); ~3.5 readings/week guarantees data in every recent window.
let private simpsonReadings =
  DemoData.simpsons ReadingRanges.defaults now
  |> List.head
  |> snd
  |> List.map (fun r -> { r with MemberId = defaultMemberId })

let private reading daysAgo (id: int) : BloodPressureReading =
  { Id = id
    MemberId = defaultMemberId
    Systolic = 120
    Diastolic = 80
    HeartRate = 66
    Timestamp = now.AddDays(-float daysAgo)
    Comments = None
    CreatedAt = now
    ModifiedAt = now }

[<Fact>]
let ``recent returns 200`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  test <@ ctx.Response.StatusCode = 200 @>

[<Fact>]
let ``recent loads a reading older than 30 days but marks its value-strip cell out-of-range`` () =
  // TODOs.md: "Recent: paning, load all data, but focus the x-axis and the value-strip
  // on last 30 days". The old reading must still be present (so panning back reveals it)
  // but its value-strip cell starts hidden via the existing out-of-range/relayout wiring.
  let tp = FakeTimeProvider(now)
  let oldReading = { reading 31 1 with Systolic = 130 }
  let recentReading = { reading 1 2 with Systolic = 125 }
  let ctx = TestHost.contextWithProvider (repoWith [ oldReading; recentReading ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx

  let cells =
    System.Text.RegularExpressions.Regex.Matches(body, "<td class=\"([^\"]*)\"[^>]*>(\\d+)</td>")
    |> Seq.map (fun m -> m.Groups[2].Value, m.Groups[1].Value)
    |> Map.ofSeq

  test <@ cells["130"].Contains "out-of-range" @>
  test <@ not (cells["125"].Contains "out-of-range") @>

[<Fact>]
let ``recent excludes a reading older than the load window entirely, even though it's out-of-range either way`` () =
  // Code review (PR #289): loading the member's *entire* lifetime history into every
  // /recent response makes the LOWESS trend line's O(n^2) precompute and the page
  // payload grow unboundedly with account age. Capping the load to a generous-but-finite
  // window keeps panning useful while bounding that cost — readings older than the load
  // window are dropped entirely (not just hidden via out-of-range).
  let tp = FakeTimeProvider(now)
  let beyondLoadWindow = { reading 400 1 with Systolic = 199 }
  let withinLoadWindow = { reading 100 2 with Systolic = 188 }

  let ctx =
    TestHost.contextWithProvider (repoWith [ beyondLoadWindow; withinLoadWindow ]) tp

  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains "199") @>
  test <@ body.Contains "188" @>

[<Fact>]
let ``recent excludes a future-dated reading entirely, since the load window's upper bound is 'now'`` () =
  // Code review (PR #289): the value strip's out-of-range check only covered the lower
  // bound (`< cutoff`), so a reading dated after `now` (clock skew, or a manually entered
  // future timestamp — BloodPressureReading.parse doesn't reject future dates) rendered as
  // if in-range even though it's beyond the chart's `rangeHigh = now` upper bound. The
  // load-window filter added in PR #290 (`ReadingStats.between` has an exclusive upper
  // bound at `now`) already excludes such readings entirely — this test pins that down.
  let tp = FakeTimeProvider(now)
  let futureReading = { reading -1 1 with Systolic = 177 }
  let inRangeReading = { reading 1 2 with Systolic = 166 }

  let ctx =
    TestHost.contextWithProvider (repoWith [ futureReading; inRangeReading ]) tp

  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains "177") @>
  test <@ body.Contains "166" @>

[<Fact>]
let ``recent renders a chart`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith simpsonReadings) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "Plotly.newPlot" @>

[<Fact>]
let ``recent renders the chart without a details wrapper`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains "<details") && body.Contains "class=\"chart-container\"" @>

[<Fact>]
let ``recent renders the chart with the authenticated member's goal range`` () =
  let goal: GoalRange =
    { SystolicMin = 100
      SystolicMax = 135
      DiastolicMin = 65
      DiastolicMax = 88 }

  let m =
    FamilyMember.create "Me" true
    |> Result.defaultWith (fun _ -> failwith "invalid member")
    |> fun m ->
        { m with
            Id = defaultMemberId
            Goal = goal }

  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithMembersAndProvider (repoWith simpsonReadings) [ m ] tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "\"y0\":100" @>
  test <@ body.Contains "\"y1\":135" @>
  test <@ body.Contains "\"y0\":65" @>
  test <@ body.Contains "\"y1\":88" @>

[<Fact>]
let ``recent heading does not repeat the member's name (already shown in the navbar)`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "<h1>Recent</h1>" @>

[<Fact>]
let ``recent renders the chart without the collapse wrapper used on history`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains "Blood Pressure Graph") && body.Contains "class=\"chart\"" @>

[<Fact>]
let ``recent shows a sys/dias value strip listing every reading in the chart window, oldest first`` () =
  let tp = FakeTimeProvider(now)

  let r1 =
    { reading 3 1 with
        Systolic = 130
        Diastolic = 82 }

  let r2 =
    { reading 2 2 with
        Systolic = 142
        Diastolic = 91 }

  let r3 =
    { reading 1 3 with
        Systolic = 118
        Diastolic = 76 }

  let ctx = TestHost.contextWithProvider (repoWith [ r1; r2; r3 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "value-strip" @>

  let systolicRow =
    body.Substring(body.IndexOf "Systolic", body.IndexOf "Diastolic" - body.IndexOf "Systolic")

  let diastolicRow = body.Substring(body.IndexOf "Diastolic")

  test
    <@
      systolicRow.IndexOf "130" < systolicRow.IndexOf "142"
      && systolicRow.IndexOf "142" < systolicRow.IndexOf "118"
    @>

  test
    <@
      diastolicRow.IndexOf "82" < diastolicRow.IndexOf "91"
      && diastolicRow.IndexOf "91" < diastolicRow.IndexOf "76"
    @>

[<Fact>]
let ``recent value strip uses a table so each reading's sys/dias values align in the same column`` () =
  let tp = FakeTimeProvider(now)

  let r1 =
    { reading 3 1 with
        Systolic = 130
        Diastolic = 82 }

  let r2 =
    { reading 2 2 with
        Systolic = 142
        Diastolic = 91 }

  let r3 =
    { reading 1 3 with
        Systolic = 118
        Diastolic = 76 }

  let ctx = TestHost.contextWithProvider (repoWith [ r1; r2; r3 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  let stripStart = body.IndexOf "value-strip"
  let stripEnd = body.IndexOf("</table>", stripStart)
  let strip = body.Substring(stripStart, stripEnd - stripStart)

  let cellValues =
    System.Text.RegularExpressions.Regex.Matches(strip, "<td[^>]*>(\\d+)</td>")
    |> Seq.map (fun m -> m.Groups[1].Value)
    |> List.ofSeq

  test <@ strip.Contains "<table" @>
  test <@ cellValues = [ "130"; "142"; "118"; "82"; "91"; "76" ] @>

[<Fact>]
let ``recent value strip tags each cell with the reading's chart x-label, for the scrubber bar to match against`` () =
  // Wegier et al. 2021, "Scrubber bar": the vertical line that tracks the cursor links
  // the chart and the data table together. The chart's hover payload reports the
  // hovered point's x as Formats.formatLocal r.Timestamp (see Charts.fs `seriesOf`), so
  // each value-strip cell needs the same string in a data-x attribute to be matchable.
  let r1 =
    { reading 3 1 with
        Systolic = 130
        Diastolic = 82 }

  let r2 =
    { reading 2 2 with
        Systolic = 142
        Diastolic = 91 }

  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r1; r2 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  let expectedX1 = Formats.formatLocal r1.Timestamp
  let expectedX2 = Formats.formatLocal r2.Timestamp

  test <@ body.Contains $"data-x=\"{expectedX1}\"" @>
  test <@ body.Contains $"data-x=\"{expectedX2}\"" @>

[<Fact>]
let ``recent chart wires a relayout listener to keep the value strip in sync with the x-axis`` () =
  // TODOs.md: "Recent: when zooming/paning keep the value-strip in sync with the
  // displayed x-axis". On zoom/pan the chart's x-range narrows; the value strip needs
  // to hide columns outside that range to stay aligned with what's plotted.
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ reading 1 1 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx

  test <@ body.Contains "plotly_relayout" @>

[<Fact>]
let ``recent value strip marks a reading above the goal range as 'above'`` () =
  // Default goal range (GoalRange.defaults): systolic max 140. 142 > 140.
  let r = { reading 1 1 with Systolic = 142 }
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "value-strip-value above" @>

[<Fact>]
let ``recent value strip marks a reading below the goal range as 'below'`` () =
  // Default goal range (GoalRange.defaults): diastolic min 60. 59 < 60.
  let r = { reading 1 1 with Diastolic = 59 }
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "value-strip-value below" @>

[<Fact>]
let ``recent value strip leaves an in-range reading's cells unmarked`` () =
  // reading helper's defaults (Systolic = 120, Diastolic = 80) are inside GoalRange.defaults.
  let r = reading 1 1
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains "value-strip-value above") @>
  test <@ not (body.Contains "value-strip-value below") @>
