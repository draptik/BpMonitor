module TrendHandlerTests

open System.Text.RegularExpressions
open Xunit
open Swensen.Unquote
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

// now = Tuesday 2026-06-09 12:00 UTC → ISO week 24, month June, year 2026
let private trendsNow = Timestamp.utc 2026 6 9 12 0 0

let private setRouteGran (ctx: HttpContext) (gran: string) =
  ctx.Request.RouteValues["gran"] <- box gran

let private setRouteGranKey (ctx: HttpContext) (gran: string) (key: string) =
  ctx.Request.RouteValues["gran"] <- box gran
  ctx.Request.RouteValues["key"] <- box key

[<Fact>]
let ``trends renders 200 with granularity buttons and current Weekly panel`` () =
  let tp = FakeTimeProvider(trendsNow)

  let r =
    { sample with
        // reading in current week (W24 starts 2026-06-08)
        Timestamp = trendsNow.AddDays(-1.0)
        Systolic = 130
        Diastolic = 85
        HeartRate = 70
    }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.trends ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  // Granularity buttons
  let weeklyGran = Routes.trendsGran (TrendPeriod.slug Weekly)
  let monthlyGran = Routes.trendsGran (TrendPeriod.slug Monthly)
  let yearlyGran = Routes.trendsGran (TrendPeriod.slug Yearly)
  test <@ body.Contains $"href=\"{weeklyGran}\"" @>
  test <@ body.Contains $"href=\"{monthlyGran}\"" @>
  test <@ body.Contains $"href=\"{yearlyGran}\"" @>
  // Weekly is active
  test <@ body.Contains "aria-current=\"page\"" @>
  // Inline chart rendered
  test <@ body.Contains "Plotly.newPlot" @>
  // Edit links return to the current period
  test <@ body.Contains(Routes.readingEdit r.Id (Routes.trendsPeriod Weekly "2026-W24")) @>

[<Fact>]
let ``trends renders the chart with the authenticated member's goal range`` () =
  let goal: GoalRange =
    {
      SystolicMin = 100
      SystolicMax = 135
      DiastolicMin = 65
      DiastolicMax = 88
    }

  let r =
    { sample with
        Timestamp = trendsNow.AddDays(-1.0)
        Systolic = 130
        Diastolic = 85
        HeartRate = 70
    }

  let tp = FakeTimeProvider(trendsNow)

  let ctx =
    TestHost.contextWithMembersAndProvider (repoWith [ r ]) [ memberWithGoal goal ] tp

  TestHost.run ReadingHandlers.trends ctx
  assertGoalBands goal (TestHost.readBody ctx)

[<Fact>]
let ``trendsPanel renders the chart with the authenticated member's goal range`` () =
  let goal: GoalRange =
    {
      SystolicMin = 100
      SystolicMax = 135
      DiastolicMin = 65
      DiastolicMax = 88
    }

  let r =
    { sample with
        Timestamp = trendsNow.AddDays(-1.0)
        Systolic = 130
        Diastolic = 85
        HeartRate = 70
    }

  let tp = FakeTimeProvider(trendsNow)

  let ctx =
    TestHost.contextWithMembersAndProvider (repoWith [ r ]) [ memberWithGoal goal ] tp

  setRouteGran ctx (TrendPeriod.slug Weekly)
  TestHost.run ReadingHandlers.trendsPanel ctx
  assertGoalBands goal (TestHost.readBody ctx)

[<Fact>]
let ``trendsPanel with gran=weekly returns fragment with sub-period buttons and stats`` () =
  let tp = FakeTimeProvider(trendsNow)

  let r =
    { sample with
        Timestamp = trendsNow.AddDays(-1.0)
        Systolic = 130
        Diastolic = 85
        HeartRate = 70
    }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  setRouteGran ctx (TrendPeriod.slug Weekly)
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  // Averages rendered
  test <@ body.Contains "130" @>
  test <@ body.Contains "85" @>
  test <@ body.Contains "70" @>
  // Sub-period buttons present (This Week, Last Week)
  test <@ body.Contains "This Week" @>
  test <@ body.Contains "Last Week" @>
  // Inline chart rendered
  test <@ body.Contains "Plotly.newPlot" @>
  // Edit links return to the current period
  test <@ body.Contains(Routes.readingEdit r.Id (Routes.trendsPeriod Weekly "2026-W24")) @>

[<Fact>]
let ``trendsPanel with gran=monthly returns monthly sub-period buttons`` () =
  let tp = FakeTimeProvider(trendsNow)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  setRouteGran ctx (TrendPeriod.slug Monthly)
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "This Month" @>
  test <@ body.Contains "Last Month" @>

[<Fact>]
let ``trendsPanel with gran + key uses that specific sub-period`` () =
  let tp = FakeTimeProvider(trendsNow)

  // Reading in W23 (last week: 2026-06-01 ... 2026-06-07)
  let r =
    { sample with
        Timestamp = Timestamp.utc 2026 6 3 9 0 0 // W23
        Systolic = 118
        Diastolic = 77
        HeartRate = 65
    }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  setRouteGranKey ctx (TrendPeriod.slug Weekly) "2026-W23"
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  // Stats for the W23 reading
  test <@ body.Contains "118" @>
  test <@ body.Contains "77" @>
  // Inline chart rendered
  test <@ body.Contains "Plotly.newPlot" @>
  // Edit links return to this period
  test <@ body.Contains(Routes.readingEdit r.Id (Routes.trendsPeriod Weekly "2026-W23")) @>

[<Fact>]
let ``trendsPanel includes readings table with in-period readings`` () =
  let tp = FakeTimeProvider(trendsNow)

  let r =
    { sample with
        Timestamp = trendsNow.AddDays(-1.0) // in current week
        Systolic = 130
    }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  setRouteGran ctx (TrendPeriod.slug Weekly)
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "id=\"readings\"" @>
  test <@ body.Contains "130" @>

[<Fact>]
let ``trendsPanel excludes readings outside the period from the table`` () =
  let tp = FakeTimeProvider(trendsNow)

  let inside =
    { sample with
        Timestamp = trendsNow.AddDays(-1.0) // current week
        Systolic = 130
    }

  let outside =
    { sample with
        Id = 2
        Timestamp = trendsNow.AddDays(-100.0) // many weeks ago
        Systolic = 999
    }

  let ctx = TestHost.contextWithProvider (repoWith [ inside; outside ]) tp
  setRouteGran ctx (TrendPeriod.slug Weekly)
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "130" @>
  test <@ body.Contains "999" |> not @>

[<Fact>]
let ``trendsPanel shows empty state when no readings in period`` () =
  let tp = FakeTimeProvider(trendsNow)

  // Reading in the distant past — outside current week
  let r =
    { sample with
        Timestamp = trendsNow.AddDays(-100.0)
    }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  setRouteGran ctx (TrendPeriod.slug Weekly)
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "No readings in" @>
  // No chart rendered when there are no readings in the period
  test <@ body.Contains "Plotly.newPlot" |> not @>

[<Fact>]
let ``trendsPanel returns 400 for unrecognized gran`` () =
  let tp = FakeTimeProvider(trendsNow)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  // No route value → gran = None → 400
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 400 @>

[<Fact>]
let ``trendsPanel with invalid gran string returns 400`` () =
  let tp = FakeTimeProvider(trendsNow)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  setRouteGran ctx "notvalid"
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 400 @>

[<Fact>]
let ``trendsPanel with an unparsable key falls back to the current period`` () =
  let tp = FakeTimeProvider(trendsNow)

  let r =
    { sample with
        Timestamp = trendsNow.AddDays(-1.0) // current week
        Systolic = 130
    }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  setRouteGranKey ctx (TrendPeriod.slug Weekly) "not-a-key"
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  // Falls back to TrendPeriod.current, so the current-week reading is in range
  test <@ body.Contains "130" @>

[<Fact>]
let ``trendsPanel period pills without data are aria-disabled and have no href`` () =
  let tp = FakeTimeProvider(trendsNow)

  // Only a reading in current week (W24) — Last Week (W23) has no data
  let r =
    { sample with
        Timestamp = trendsNow.AddDays(-1.0) // W24
        Systolic = 130
    }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  setRouteGran ctx (TrendPeriod.slug Weekly)
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  let w24Route = Routes.trendsGranKey (TrendPeriod.slug Weekly) "2026-W24"
  let w23Route = Routes.trendsGranKey (TrendPeriod.slug Weekly) "2026-W23"
  // W24 (This Week) pill should be a normal link
  test <@ body.Contains $"href=\"{w24Route}\"" @>
  // W23 (Last Week) pill should be disabled — no href, has aria-disabled
  test <@ body.Contains $"href=\"{w23Route}\"" |> not @>
  test <@ body.Contains "aria-disabled=\"true\"" @>

[<Fact>]
let ``trends page opens the granularity and period named in its query`` () =
  let tp = FakeTimeProvider(trendsNow)

  let r =
    { sample with
        Timestamp = Timestamp.utc 2026 6 3 9 0 0 // W23, last week
        Systolic = 118
    }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  ctx.Request.QueryString <- QueryString "?gran=weekly&key=2026-W23"
  TestHost.run ReadingHandlers.trends ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "<h1>" @>
  test <@ body.Contains "118" @>

[<Fact>]
let ``trends page opens a non-default granularity named in its query`` () =
  let tp = FakeTimeProvider(trendsNow)

  let r =
    { sample with
        Timestamp = Timestamp.utc 2026 4 15 9 0 0
        Systolic = 119
    }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  ctx.Request.QueryString <- QueryString "?gran=monthly&key=2026-04"
  TestHost.run ReadingHandlers.trends ctx

  let body = TestHost.readBody ctx
  let april = Routes.trendsGranKey (TrendPeriod.slug Monthly) "2026-04"
  test <@ Regex.IsMatch(body, $"<a href=\"{april}\"[^>]*aria-current=\"page\"") @>
  test <@ body.Contains "119" @>

[<Fact>]
let ``trends page opens the current period for a key outside the period strip`` () =
  let tp = FakeTimeProvider(trendsNow)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  ctx.Request.QueryString <- QueryString "?gran=weekly&key=2020-W10"
  TestHost.run ReadingHandlers.trends ctx

  let body = TestHost.readBody ctx
  let w24 = Routes.trendsGranKey (TrendPeriod.slug Weekly) "2026-W24"
  test <@ Regex.IsMatch(body, $"<a href=\"{w24}\"[^>]*aria-current=\"page\"") @>

[<Fact>]
let ``trendsPanel opens the current period for a key outside the period strip`` () =
  let tp = FakeTimeProvider(trendsNow)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  setRouteGranKey ctx (TrendPeriod.slug Weekly) "2020-W10"
  TestHost.run ReadingHandlers.trendsPanel ctx

  let body = TestHost.readBody ctx
  let w24 = Routes.trendsGranKey (TrendPeriod.slug Weekly) "2026-W24"
  test <@ Regex.IsMatch(body, $"<a href=\"{w24}\"[^>]*aria-current=\"page\"") @>
