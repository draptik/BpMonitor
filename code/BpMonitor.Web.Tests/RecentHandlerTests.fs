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
let ``recent omits a reading older than 30 days`` () =
  let tp = FakeTimeProvider(now)
  let r = reading 31 1
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains "/readings/1/edit") @>

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
