module ReadingHandlerTests

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
  test <@ body.Contains "href=\"/add\"" && body.Contains "href=\"/history\"" @>

[<Fact>]
let ``history renders a row per reading`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.run ReadingHandlers.history ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ (TestHost.readBody ctx).Contains "/readings/1/edit" @>

[<Fact>]
let ``history renders the chart with the authenticated member's goal range`` () =
  let goal =
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

  let repo = repoWith [ sample ]
  let ctx = TestHost.contextWithMembers repo [ m ]
  TestHost.run ReadingHandlers.history ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "\"y0\":100" @>
  test <@ body.Contains "\"y1\":135" @>
  test <@ body.Contains "\"y0\":65" @>
  test <@ body.Contains "\"y1\":88" @>

[<Fact>]
let ``newReading returns 200 and prefills timestamp with local time`` () =
  let tp = FakeTimeProvider(Timestamp.utc 2026 6 9 8 0 0)
  let ctx = TestHost.contextWithProvider (repoWith []) tp

  TestHost.run ReadingHandlers.newReading ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let expected = tp.GetLocalNow().ToString(Formats.timestamp)
  test <@ (TestHost.readBody ctx).Contains $"value=\"{expected}\"" @>

[<Fact>]
let ``createReading persists a valid reading and redirects`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm
    ctx
    [ "Timestamp", "2026-05-01 09:00"
      "Systolic", "120"
      "Diastolic", "80"
      "HeartRate", "66"
      "Comments", "x" ]

  TestHost.run ReadingHandlers.createReading ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/history" @>
  test <@ repo.GetAll(defaultMemberId) |> List.length = 1 @>

[<Fact>]
let ``createReading stamps reading with active member Id`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm
    ctx
    [ "Timestamp", "2026-05-01 09:00"
      "Systolic", "120"
      "Diastolic", "80"
      "HeartRate", "66"
      "Comments", "" ]

  TestHost.run ReadingHandlers.createReading ctx

  test <@ repo.GetAll(defaultMemberId).[0].MemberId = defaultMemberId @>

[<Fact>]
let ``createReading rejects an out-of-range reading with 422 and does not persist`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm
    ctx
    [ "Timestamp", "2026-05-01 09:00"
      "Systolic", "999"
      "Diastolic", "80"
      "HeartRate", "66"
      "Comments", "" ]

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
    [ "Timestamp", "2026-05-01 09:00"
      "Systolic", "abc"
      "Diastolic", "80"
      "HeartRate", "66"
      "Comments", "" ]

  TestHost.run ReadingHandlers.createReading ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "not a valid integer" @>
  test <@ repo.GetAll(defaultMemberId) |> List.isEmpty @>

[<Fact>]
let ``editReading prefills the form for an existing reading`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1
  TestHost.run ReadingHandlers.editReading ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ (TestHost.readBody ctx).Contains "value=\"120\"" @>

[<Fact>]
let ``editReading returns 404 for an unknown id`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 999
  TestHost.run ReadingHandlers.editReading ctx

  test <@ ctx.Response.StatusCode = 404 @>

[<Fact>]
let ``updateReading saves changes and redirects`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1

  TestHost.setForm
    ctx
    [ "Timestamp", "2026-05-01 09:00"
      "Systolic", "111"
      "Diastolic", "70"
      "HeartRate", "60"
      "Comments", "updated" ]

  TestHost.run ReadingHandlers.updateReading ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/history" @>
  let updated = repo.GetAll(defaultMemberId) |> List.exactlyOne
  test <@ updated.Systolic = 111 && updated.Comments = Some "updated" @>
