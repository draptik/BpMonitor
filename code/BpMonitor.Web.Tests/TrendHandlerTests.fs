module TrendHandlerTests

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
        HeartRate = 70 }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  TestHost.run ReadingHandlers.trends ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  // Granularity buttons
  test <@ body.Contains "href=\"/trends/weekly\"" @>
  test <@ body.Contains "href=\"/trends/monthly\"" @>
  test <@ body.Contains "href=\"/trends/yearly\"" @>
  // Weekly is active
  test <@ body.Contains "aria-current=\"page\"" @>
  // Chart iframe uses new gran/period params
  test <@ body.Contains "gran=weekly" @>

[<Fact>]
let ``trendsPanel with gran=weekly returns fragment with sub-period buttons and stats`` () =
  let tp = FakeTimeProvider(trendsNow)

  let r =
    { sample with
        Timestamp = trendsNow.AddDays(-1.0)
        Systolic = 130
        Diastolic = 85
        HeartRate = 70 }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  setRouteGran ctx "weekly"
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
  // Chart iframe uses gran + period params
  test <@ body.Contains "gran=weekly" @>

[<Fact>]
let ``trendsPanel with gran=monthly returns monthly sub-period buttons`` () =
  let tp = FakeTimeProvider(trendsNow)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  setRouteGran ctx "monthly"
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "This Month" @>
  test <@ body.Contains "Last Month" @>

[<Fact>]
let ``trendsPanel with gran + key uses that specific sub-period`` () =
  let tp = FakeTimeProvider(trendsNow)

  // Reading in W23 (last week: 2026-06-01 .. 2026-06-07)
  let r =
    { sample with
        Timestamp = Timestamp.utc 2026 6 3 9 0 0 // W23
        Systolic = 118
        Diastolic = 77
        HeartRate = 65 }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  setRouteGranKey ctx "weekly" "2026-W23"
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  // Stats for the W23 reading
  test <@ body.Contains "118" @>
  test <@ body.Contains "77" @>
  // Period key is in the chart URL
  test <@ body.Contains "period=2026-W23" @>

[<Fact>]
let ``trendsPanel includes readings table with in-period readings`` () =
  let tp = FakeTimeProvider(trendsNow)

  let r =
    { sample with
        Timestamp = trendsNow.AddDays(-1.0) // in current week
        Systolic = 130 }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  setRouteGran ctx "weekly"
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
        Systolic = 130 }

  let outside =
    { sample with
        Id = 2
        Timestamp = trendsNow.AddDays(-100.0) // many weeks ago
        Systolic = 999 }

  let ctx = TestHost.contextWithProvider (repoWith [ inside; outside ]) tp
  setRouteGran ctx "weekly"
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
        Timestamp = trendsNow.AddDays(-100.0) }

  let ctx = TestHost.contextWithProvider (repoWith [ r ]) tp
  setRouteGran ctx "weekly"
  TestHost.run ReadingHandlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "No readings in" @>
  test <@ body.Contains "gran=weekly" |> not @>

[<Fact>]
let ``trendsPanel returns 400 for unrecognised gran`` () =
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
