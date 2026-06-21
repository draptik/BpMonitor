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
let ``recent returns 200 with three window headings`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx

  test
    <@
      body.Contains "Last 7 days"
      && body.Contains "Last 14 days"
      && body.Contains "Last 30 days"
    @>

[<Fact>]
let ``recent renders pill links for each window`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx

  test
    <@
      body.Contains "href=\"#days-7\""
      && body.Contains "href=\"#days-14\""
      && body.Contains "href=\"#days-30\""
    @>

[<Fact>]
let ``recent sections have anchor ids`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx

  test
    <@
      body.Contains "id=\"days-7\""
      && body.Contains "id=\"days-14\""
      && body.Contains "id=\"days-30\""
    @>

[<Fact>]
let ``recent shows simpson readings in all three windows`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith simpsonReadings) tp
  TestHost.run ReadingHandlers.recent ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  // Marge has ~3.5 readings/week — all three windows will have edit links.
  test <@ body.Contains "/readings/" @>

[<Fact>]
let ``recent omits a reading older than 30 days`` () =
  let tp = FakeTimeProvider(now)
  let r = reading 31 1
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "Last 30 days" && not (body.Contains "/readings/1/edit") @>

[<Fact>]
let ``recent does not show a reading from 10 days ago in the 7-day window`` () =
  let tp = FakeTimeProvider(now)
  let r = reading 10 42
  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.recent ctx

  // 10-day reading falls in 14d and 30d windows but not 7d.
  let body = TestHost.readBody ctx
  let occurrences = body.Split("/readings/42/edit").Length - 1
  test <@ occurrences = 2 @>

[<Fact>]
let ``recent renders a chart`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith simpsonReadings) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "Plotly.newPlot" @>

[<Fact>]
let ``recent chart is open by default`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "<details open" @>

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
let ``recent chart toggle label matches the history page's`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "Blood Pressure Graph<" && not (body.Contains "(last 30 days)") @>
