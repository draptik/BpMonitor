module ReadingHandlerTests

open System.Text.RegularExpressions
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Web
open HandlerTestHelpers

[<Fact>]
let ``landing renders links to add and history`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo
  TestHost.run ReadingHandlers.landing ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx

  test
    <@
      body.Contains $"href=\"{Routes.add}\""
      && body.Contains $"href=\"{Routes.history}\""
    @>

[<Fact>]
let ``history renders a row per reading`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.run ReadingHandlers.history ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ (TestHost.readBody ctx).Contains(Routes.readingEdit 1 Routes.history) @>

[<Fact>]
let ``history renders the chart with the authenticated member's goal range`` () =
  let goal =
    {
      SystolicMin = 100
      SystolicMax = 135
      DiastolicMin = 65
      DiastolicMax = 88
    }

  let ctx = TestHost.contextWithMembers (repoWith [ sample ]) [ memberWithGoal goal ]
  TestHost.run ReadingHandlers.history ctx
  assertGoalBands goal (TestHost.readBody ctx)

[<Fact>]
let ``newReading returns 200 and prefills timestamp with local time`` () =
  let tp = FakeTimeProvider(Timestamp.utc 2026 6 9 8 0 0)
  let ctx = TestHost.contextWithProvider (repoWith []) tp

  TestHost.run ReadingHandlers.newReading ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let expected = tp.GetLocalNow().ToString(Formats.timestamp)
  test <@ (TestHost.readBody ctx).Contains $"value=\"{expected}\"" @>

[<Fact>]
let ``newReading prefills the timestamp in the Gregorian calendar, whatever the server culture`` () =
  let tp = FakeTimeProvider(Timestamp.utc 2026 6 9 8 0 0)
  let ctx = TestHost.contextWithProvider (repoWith []) tp

  BpMonitor.TestSupport.CultureScope.withCulture "th-TH" (fun () -> TestHost.run ReadingHandlers.newReading ctx)

  let expected =
    tp.GetLocalNow().ToString(Formats.timestamp, System.Globalization.CultureInfo.InvariantCulture)

  test <@ (TestHost.readBody ctx).Contains $"value=\"{expected}\"" @>

[<Fact>]
let ``createReading persists a valid reading and redirects`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "120"
      FormFields.diastolic, "80"
      FormFields.heartRate, "66"
      FormFields.comments, "x"
    ]

  TestHost.run ReadingHandlers.createReading ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.recent @>
  test <@ repo.GetAll(defaultMemberId) |> List.length = 1 @>

[<Fact>]
let ``createReading stamps reading with active member Id`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "120"
      FormFields.diastolic, "80"
      FormFields.heartRate, "66"
      FormFields.comments, ""
    ]

  TestHost.run ReadingHandlers.createReading ctx

  test <@ repo.GetAll(defaultMemberId).[0].MemberId = defaultMemberId @>

[<Fact>]
let ``createReading rejects an out-of-range reading with 422 and does not persist`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "999"
      FormFields.diastolic, "80"
      FormFields.heartRate, "66"
      FormFields.comments, ""
    ]

  TestHost.run ReadingHandlers.createReading ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "out of range" @>
  test <@ repo.GetAll(defaultMemberId) |> List.isEmpty @>

[<Fact>]
let ``createReading rejects a non-numeric field with 422`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "abc"
      FormFields.diastolic, "80"
      FormFields.heartRate, "66"
      FormFields.comments, ""
    ]

  TestHost.run ReadingHandlers.createReading ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "not a valid integer" @>
  test <@ repo.GetAll(defaultMemberId) |> List.isEmpty @>

[<Fact>]
let ``recent marks only the Last 30 days button as active`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.run ReadingHandlers.recent ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx

  let zoomButtons =
    Regex.Matches(body, "<button[^>]*recent-zoom-button[^>]*>([^<]*)</button>")
    |> Seq.cast<Match>
    |> Seq.map (fun m -> m.Value.Contains "aria-pressed=\"true\"", m.Groups[1].Value)
    |> Seq.toList

  test <@ zoomButtons |> List.filter fst |> List.length = 1 @>

  test
    <@
      zoomButtons
      |> List.exists (fun (pressed, label) -> pressed && label = "Last 30 days")
    @>

[<Fact>]
let ``editReading prefills the form for an existing reading`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1
  TestHost.run ReadingHandlers.editReading ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ (TestHost.readBody ctx).Contains "value=\"120\"" @>

[<Theory>]
[<InlineData("/recent", "/recent")>]
[<InlineData("javascript:alert(1)", "/history")>]
[<InlineData("/trends?gran=monthly&key=2026-05", "/trends?gran=monthly&amp;key=2026-05")>]
[<InlineData("/trends?gran=monthly&key=2026-05\"", "/trends")>]
let ``editReading hands an allowlisted return target to the form`` (requested: string, expected: string) =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1
  ctx.Request.QueryString <- Microsoft.AspNetCore.Http.QueryString.Create(FormFields.returnTo, requested)
  TestHost.run ReadingHandlers.editReading ctx

  test <@ (TestHost.readBody ctx).Contains $"name=\"{FormFields.returnTo}\" value=\"{expected}\"" @>

[<Fact>]
let ``editReading returns 404 for an unknown id`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 999
  TestHost.run ReadingHandlers.editReading ctx

  test <@ ctx.Response.StatusCode = 404 @>

[<Fact>]
let ``editReading returns 404 for a reading belonging to a different member`` () =
  let other = { sample with MemberId = 999 }
  let repo = repoWith [ other ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx other.Id
  TestHost.run ReadingHandlers.editReading ctx

  test <@ ctx.Response.StatusCode = 404 @>

[<Fact>]
let ``updateReading saves changes and redirects`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "111"
      FormFields.diastolic, "70"
      FormFields.heartRate, "60"
      FormFields.comments, "updated"
    ]

  TestHost.run ReadingHandlers.updateReading ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = Routes.history @>
  let updated = repo.GetAll(defaultMemberId) |> List.exactlyOne
  test <@ updated.Systolic = 111 && updated.Comments = Some "updated" @>

let private updateReturningTo (returnTo: string) =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "111"
      FormFields.diastolic, "70"
      FormFields.heartRate, "60"
      FormFields.comments, ""
      FormFields.returnTo, returnTo
    ]

  TestHost.run ReadingHandlers.updateReading ctx
  ctx.Response.Headers.Location.ToString()

let returnTargets: obj array seq =
  Routes.readingReturnTargets |> Seq.map (fun page -> [| box page |])

[<Theory>]
[<MemberData(nameof returnTargets)>]
let ``updateReading redirects back to the page the edit was started from`` (page: string) =
  test <@ updateReturningTo page = page @>

[<Theory>]
[<InlineData("https://evil.example")>]
[<InlineData("//evil.example")>]
[<InlineData("/settings")>]
let ``updateReading falls back to history for an unrecognized return target`` (returnTo: string) =
  test <@ updateReturningTo returnTo = Routes.history @>

[<Fact>]
let ``updateReading redirects back to the Trends period the edit was started from`` () =
  let period = Routes.trendsPeriod Monthly "2026-05"
  test <@ updateReturningTo period = period @>

[<Theory>]
[<InlineData("/trends?key=2026-05&gran=monthly", "/trends?gran=monthly&key=2026-05")>]
[<InlineData("/trends?gran=Monthly&key=2026-05", "/trends?gran=monthly&key=2026-05")>]
[<InlineData("/trends?gran=weekly&key=2026-W23&x=1", "/trends?gran=weekly&key=2026-W23")>]
[<InlineData("/trends?key=2026-W23", "/trends?gran=weekly&key=2026-W23")>]
let ``updateReading returns to the canonical URL of the Trends period the page would show``
  (returnTo: string, expected: string)
  =
  test <@ updateReturningTo returnTo = expected @>

[<Theory>]
[<InlineData("/trends?gran=x\"><script>alert(1)</script>")>]
[<InlineData("/trends?gran=weekly&key=2026-W5")>]
[<InlineData("/trends?gran=monthly")>]
let ``updateReading falls back to plain Trends for a query that selects no valid period`` (returnTo: string) =
  test <@ updateReturningTo returnTo = Routes.trends @>

[<Fact>]
let ``updateReading keeps the reading's original CreatedAt`` () =
  let createdAt = System.DateTimeOffset(2026, 5, 1, 9, 5, 0, System.TimeSpan.Zero)
  let repo = repoWith [ { sample with CreatedAt = createdAt } ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "111"
      FormFields.diastolic, "70"
      FormFields.heartRate, "60"
      FormFields.comments, ""
    ]

  TestHost.run ReadingHandlers.updateReading ctx

  test <@ (repo.GetAll(defaultMemberId) |> List.exactlyOne).CreatedAt = createdAt @>

[<Fact>]
let ``updateReading rejects an out-of-range reading with 422, keeps the stored reading, and posts back to it`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "999"
      FormFields.diastolic, "70"
      FormFields.heartRate, "60"
      FormFields.comments, ""
    ]

  TestHost.run ReadingHandlers.updateReading ctx

  let body = TestHost.readBody ctx
  test <@ ctx.Response.StatusCode = 422 @>
  test <@ body.Contains "out of range" @>
  test <@ body.Contains $"action=\"{Routes.readingUpdate 1}\"" @>
  test <@ repo.GetAll(defaultMemberId) = [ sample ] @>

[<Fact>]
let ``updateReading keeps the return target when re-rendering a rejected form`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "999"
      FormFields.diastolic, "70"
      FormFields.heartRate, "60"
      FormFields.comments, ""
      FormFields.returnTo, Routes.trends
    ]

  TestHost.run ReadingHandlers.updateReading ctx

  let body = TestHost.readBody ctx
  test <@ ctx.Response.StatusCode = 422 @>
  test <@ body.Contains $"name=\"{FormFields.returnTo}\" value=\"{Routes.trends}\"" @>

[<Fact>]
let ``updateReading rejects a non-numeric field with 422 and keeps the stored reading`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "abc"
      FormFields.diastolic, "70"
      FormFields.heartRate, "60"
      FormFields.comments, ""
    ]

  TestHost.run ReadingHandlers.updateReading ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "not a valid integer" @>
  test <@ repo.GetAll(defaultMemberId) = [ sample ] @>

[<Fact>]
let ``updateReading returns 404 for an unknown id`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 999

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "111"
      FormFields.diastolic, "70"
      FormFields.heartRate, "60"
      FormFields.comments, ""
    ]

  TestHost.run ReadingHandlers.updateReading ctx

  test <@ ctx.Response.StatusCode = 404 @>

[<Fact>]
let ``updateReading does not modify a reading belonging to a different member`` () =
  let other = { sample with MemberId = 999 }
  let repo = repoWith [ other ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx other.Id

  TestHost.setForm
    ctx
    [
      FormFields.timestamp, "2026-05-01 09:00"
      FormFields.systolic, "111"
      FormFields.diastolic, "70"
      FormFields.heartRate, "60"
      FormFields.comments, ""
    ]

  TestHost.run ReadingHandlers.updateReading ctx

  test <@ ctx.Response.StatusCode = 404 @>
  test <@ (repo.GetAll(999) |> List.exactlyOne).Systolic = other.Systolic @>
