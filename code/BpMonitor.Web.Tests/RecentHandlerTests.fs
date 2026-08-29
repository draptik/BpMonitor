module RecentHandlerTests

open System.Text.RegularExpressions
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers
open RecentTestFixtures

// Marge Simpson's readings for the last 5 years anchored to `now`, stamped as member 1.
// Deterministic (fixed seed 1); ~3.5 readings/week guarantees data in every recent window.
let private simpsonReadings =
  DemoData.simpsons ReadingRanges.defaults now
  |> List.head
  |> snd
  |> List.map (fun r -> { r with MemberId = defaultMemberId })

[<Fact>]
let ``recent returns 200`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  test <@ ctx.Response.StatusCode = 200 @>

[<Fact>]
let ``recent excludes a reading older than the load window entirely, even though it's out-of-range either way`` () =
  // PR #289: caps the load window so page/LOWESS cost don't grow with account age — readings past it are dropped, not just hidden.
  let tp = FakeTimeProvider(now)
  let beyondLoadWindow = { reading 400 1 with Systolic = 199 }
  let withinLoadWindow = { reading 100 2 with Systolic = 188 }

  let ctx =
    TestHost.contextWithProvider (repoWith [ beyondLoadWindow; withinLoadWindow ]) tp

  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains ">199<") @>
  test <@ body.Contains ">188<" @>

[<Fact>]
let ``recent excludes a future-dated reading entirely, since the load window's upper bound is 'now'`` () =
  // PR #289/290: the load window's exclusive upper bound at `now` excludes future-dated readings entirely.
  let tp = FakeTimeProvider(now)
  let futureReading = { reading -1 1 with Systolic = 177 }
  let inRangeReading = { reading 1 2 with Systolic = 166 }

  let ctx =
    TestHost.contextWithProvider (repoWith [ futureReading; inRangeReading ]) tp

  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains ">177<") @>
  test <@ body.Contains ">166<" @>

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
  // Unlike /history, the chart itself isn't collapsible; the page's only <details>
  // is the readings section below the chart, not a wrapper around it.
  test <@ body.Contains "class=\"chart-container\"" @>
  test <@ Regex.Matches(body, "<details").Count = 1 @>
  test <@ body.IndexOf "<details" > body.IndexOf "<div class=\"chart\"" @>

[<Fact>]
let ``recent renders the chart with the authenticated member's goal range`` () =
  let goal: GoalRange =
    { SystolicMin = 100
      SystolicMax = 135
      DiastolicMin = 65
      DiastolicMax = 88 }

  let tp = FakeTimeProvider(now)

  let ctx =
    TestHost.contextWithMembersAndProvider (repoWith simpsonReadings) [ memberWithGoal goal ] tp

  TestHost.run ReadingHandlers.recent ctx
  assertGoalBands goal (TestHost.readBody ctx)

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
let ``recent renders zoom shortcut buttons for the last 7 and 30 days, anchored to now`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  let hi = Formats.formatLocal now
  let lo7 = Formats.formatLocal (now.AddDays(-7.0))
  let lo30 = Formats.formatLocal (now.AddDays(-30.0))

  test <@ body.Contains $"data-lo=\"{lo7}\" data-hi=\"{hi}\"" @>
  test <@ body.Contains $"data-lo=\"{lo30}\" data-hi=\"{hi}\"" @>
  test <@ body.Contains "Last 7 days" @>
  test <@ body.Contains "Last 30 days" @>

[<Fact>]
let ``recent renders a 'Load full history' button when a reading older than the load window exists`` () =
  let tp = FakeTimeProvider(now)
  let beyondLoadWindow = reading 400 1
  let withinLoadWindow = reading 1 2

  let ctx =
    TestHost.contextWithProvider (repoWith [ beyondLoadWindow; withinLoadWindow ]) tp

  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "Load full history" @>
  test <@ body.Contains $"hx-get=\"{Routes.recentFull}\"" @>

[<Fact>]
let ``recent omits the 'Load full history' button when all readings are within the load window`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ reading 100 1; reading 1 2 ]) tp
  TestHost.run ReadingHandlers.recent ctx

  let body = TestHost.readBody ctx
  test <@ not (body.Contains "Load full history") @>

[<Fact>]
let ``recentFull includes a reading older than the load window`` () =
  let tp = FakeTimeProvider(now)
  let beyondLoadWindow = { reading 400 1 with Systolic = 199 }
  let ctx = TestHost.contextWithProvider (repoWith [ beyondLoadWindow ]) tp
  TestHost.run ReadingHandlers.recentFull ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains ">199<" @>

[<Fact>]
let ``recentFull returns the chart fragment without the page shell, and omits the load-full button`` () =
  let tp = FakeTimeProvider(now)
  let ctx = TestHost.contextWithProvider (repoWith [ reading 400 1 ]) tp
  TestHost.run ReadingHandlers.recentFull ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "id=\"recent-chart\"" @>
  test <@ not (body.Contains "<h1>Recent</h1>") @>
  test <@ not (body.Contains "<nav") @>
  test <@ not (body.Contains "Load full history") @>
