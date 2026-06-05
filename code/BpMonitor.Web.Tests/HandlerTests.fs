module HandlerTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.DependencyInjection
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Web

// All tests use member 1 — the default member seeded by InMemoryFamilyMemberRepository.
let private defaultMemberId = 1

let private sample: BloodPressureReading =
  { Id = 1
    MemberId = defaultMemberId
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

  TestHost.run Handlers.createReading ctx

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

  TestHost.run Handlers.createReading ctx

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

  TestHost.run Handlers.createReading ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "not a valid integer" @>
  test <@ repo.GetAll(defaultMemberId) |> List.isEmpty @>

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
  let updated = repo.GetAll(defaultMemberId) |> List.exactlyOne
  test <@ updated.Systolic = 111 && updated.Comments = Some "updated" @>

// ─── Member handler tests ─────────────────────────────────────────────────────

let private adminMember (id: int) (name: string) : BpMonitor.Core.FamilyMember =
  { Id = id
    Name = name
    IsAdmin = true
    IsActive = true
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

[<Fact>]
let ``createMember with IsAdmin checkbox persists an admin member`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm ctx [ "Name", "Alice"; "IsAdmin", "on" ]
  TestHost.run Handlers.createMember ctx

  test <@ ctx.Response.StatusCode = 302 @>
  // The newly added member should be accessible via the member repo; verify via editMember prefill.
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let added = memberRepo.GetAll() |> List.find (fun m -> m.Name = "Alice")
  test <@ added.IsAdmin = true @>
  test <@ added.IsActive = true @>

[<Fact>]
let ``createMember without IsAdmin checkbox persists a non-admin member`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo

  TestHost.setForm ctx [ "Name", "Bob" ]
  TestHost.run Handlers.createMember ctx

  test <@ ctx.Response.StatusCode = 302 @>

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let added = memberRepo.GetAll() |> List.find (fun m -> m.Name = "Bob")
  test <@ added.IsAdmin = false @>

[<Fact>]
let ``editMember prefills the form for an existing member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me" ]
  TestHost.setRouteId ctx 1
  TestHost.run Handlers.editMember ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ (TestHost.readBody ctx).Contains "value=\"Me\"" @>

[<Fact>]
let ``editMember returns 404 for an unknown id`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me" ]
  TestHost.setRouteId ctx 999
  TestHost.run Handlers.editMember ctx

  test <@ ctx.Response.StatusCode = 404 @>

[<Fact>]
let ``updateMember saves changes and redirects to /members`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me" ]
  TestHost.setRouteId ctx 1

  TestHost.setForm ctx [ "Name", "Myself"; "IsAdmin", "on"; "IsActive", "on" ]
  TestHost.run Handlers.updateMember ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/members" @>

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let updated = memberRepo.GetAll() |> List.exactlyOne
  test <@ updated.Name = "Myself" @>

[<Fact>]
let ``updateMember rejects demoting the last active admin with 422`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me" ]
  TestHost.setRouteId ctx 1

  // Uncheck both IsAdmin and IsActive — would leave no active admin.
  TestHost.setForm ctx [ "Name", "Me" ]
  TestHost.run Handlers.updateMember ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "active admin" @>

[<Fact>]
let ``updateMember allows demoting one admin when another active admin exists`` () =
  let secondAdmin: FamilyMember =
    { Id = 2
      Name = "Alice"
      IsAdmin = true
      IsActive = true
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me"; secondAdmin ]
  TestHost.setRouteId ctx 1

  // Demote member 1 to non-admin; member 2 remains active admin → invariant holds.
  TestHost.setForm ctx [ "Name", "Me"; "IsActive", "on" ]
  TestHost.run Handlers.updateMember ctx

  test <@ ctx.Response.StatusCode = 302 @>
