module HandlerTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Web

let private sample: BloodPressureReading =
  { Id = 1
    Systolic = 120
    Diastolic = 80
    HeartRate = 66
    Timestamp = Timestamp.utc 2026 5 1 9 0 0
    Comments = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let private repoWith readings : IReadingRepository =
  InMemoryReadingRepository(Some readings)

[<Fact>]
let ``landing renders links to add and history`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo
  TestHost.run Handlers.landing ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "href=\"/add\"" && body.Contains "href=\"/history\"" @>

[<Fact>]
let ``history renders a row per reading`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.run Handlers.history ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ (TestHost.readBody ctx).Contains "/readings/1/edit" @>

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

  TestHost.run Handlers.createReading ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/history" @>
  test <@ repo.GetAll() |> List.length = 1 @>

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

  TestHost.run Handlers.createReading ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "out of range" @>
  test <@ repo.GetAll() |> List.isEmpty @>

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

  TestHost.run Handlers.createReading ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "not a valid integer" @>
  test <@ repo.GetAll() |> List.isEmpty @>

[<Fact>]
let ``editReading prefills the form for an existing reading`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 1
  TestHost.run Handlers.editReading ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ (TestHost.readBody ctx).Contains "value=\"120\"" @>

[<Fact>]
let ``editReading returns 404 for an unknown id`` () =
  let repo = repoWith [ sample ]
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 999
  TestHost.run Handlers.editReading ctx

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

  TestHost.run Handlers.updateReading ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/history" @>
  let updated = repo.GetAll() |> List.exactlyOne
  test <@ updated.Systolic = 111 && updated.Comments = Some "updated" @>
