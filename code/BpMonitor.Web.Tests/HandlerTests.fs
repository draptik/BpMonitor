module HandlerTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Time.Testing
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
let ``newReading returns 200 and prefills timestamp with local time`` () =
  let tp = FakeTimeProvider(Timestamp.utc 2026 6 9 8 0 0)
  let ctx = TestHost.contextWithProvider (repoWith []) tp

  TestHost.run Handlers.newReading ctx

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
    PasswordHash = None
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
      PasswordHash = None
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ adminMember 1 "Me"; secondAdmin ]
  TestHost.setRouteId ctx 1

  // Demote member 1 to non-admin; member 2 remains active admin → invariant holds.
  TestHost.setForm ctx [ "Name", "Me"; "IsActive", "on" ]
  TestHost.run Handlers.updateMember ctx

  test <@ ctx.Response.StatusCode = 302 @>

// ─── Login handler tests ──────────────────────────────────────────────────────

let private unclaimedMember: FamilyMember =
  { Id = 1
    Name = "Me"
    IsAdmin = true
    IsActive = true
    PasswordHash = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let private claimedMember (hash: string) : FamilyMember =
  { unclaimedMember with
      PasswordHash = Some hash }

[<Fact>]
let ``loginPage returns 200 with sign-in form`` () =
  let ctx = TestHost.context (repoWith [])
  TestHost.run Handlers.loginPage ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ (TestHost.readBody ctx).Contains "Sign in" @>

[<Fact>]
let ``loginWithCredentials redirects to / for correct credentials`` () =
  let hash = PasswordHashing.hash "correct"

  let claimed = { claimedMember hash with Name = "Me" }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimed ]
  TestHost.setForm ctx [ "Username", "Me"; "Password", "correct" ]
  TestHost.run Handlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/" @>

[<Fact>]
let ``loginWithCredentials returns 401 for wrong password`` () =
  let hash = PasswordHashing.hash "correct"

  let claimed = { claimedMember hash with Name = "Me" }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimed ]
  TestHost.setForm ctx [ "Username", "Me"; "Password", "wrong" ]
  TestHost.run Handlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 401 @>

[<Fact>]
let ``loginWithCredentials returns 401 for unknown user`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo
  TestHost.setForm ctx [ "Username", "Nobody"; "Password", "anything" ]
  TestHost.run Handlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 401 @>

[<Fact>]
let ``loginWithCredentials redirects to claim page for unclaimed member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setForm ctx [ "Username", "Me"; "Password", "" ]
  TestHost.run Handlers.loginWithCredentials ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/login/1" @>

[<Fact>]
let ``loginMember returns 200 for an existing active member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1
  TestHost.run Handlers.loginMember ctx

  test <@ ctx.Response.StatusCode = 200 @>

[<Fact>]
let ``loginMember returns 404 for unknown member`` () =
  let repo = repoWith []
  let ctx = TestHost.context repo
  TestHost.setRouteId ctx 999
  TestHost.run Handlers.loginMember ctx

  test <@ ctx.Response.StatusCode = 404 @>

[<Fact>]
let ``loginMember returns 403 for inactive member`` () =
  let inactive =
    { unclaimedMember with
        IsActive = false }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ inactive ]
  TestHost.setRouteId ctx 1
  TestHost.run Handlers.loginMember ctx

  test <@ ctx.Response.StatusCode = 403 @>

[<Fact>]
let ``loginSubmit claims unclaimed member and sets password hash`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", "correct-horse"; "PasswordConfirm", "correct-horse" ]
  TestHost.run Handlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 302 @>
  // Verify the password hash was persisted
  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  let saved = memberRepo.GetById(1) |> Option.get
  test <@ BpMonitor.Core.FamilyMember.isClaimed saved @>
  test <@ PasswordHashing.verify "correct-horse" (saved.PasswordHash |> Option.get) @>

[<Fact>]
let ``loginSubmit rejects empty password for unclaimed member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", ""; "PasswordConfirm", "" ]
  TestHost.run Handlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "empty" @>

[<Fact>]
let ``loginSubmit rejects mismatched confirm for unclaimed member`` () =
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ unclaimedMember ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", "abc"; "PasswordConfirm", "xyz" ]
  TestHost.run Handlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 422 @>
  test <@ (TestHost.readBody ctx).Contains "do not match" @>

[<Fact>]
let ``loginSubmit accepts correct password for claimed member and redirects`` () =
  let hash = PasswordHashing.hash "letmein"
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimedMember hash ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", "letmein" ]
  TestHost.run Handlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 302 @>
  test <@ ctx.Response.Headers.Location.ToString() = "/" @>

[<Fact>]
let ``loginSubmit rejects wrong password for claimed member with 401`` () =
  let hash = PasswordHashing.hash "letmein"
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimedMember hash ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", "wrongpassword" ]
  TestHost.run Handlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 401 @>
  test <@ (TestHost.readBody ctx).Contains "Incorrect password" @>

[<Fact>]
let ``loginSubmit returns 403 for inactive member`` () =
  let hash = PasswordHashing.hash "secret"

  let inactive =
    { claimedMember hash with
        IsActive = false }

  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ inactive ]
  TestHost.setRouteId ctx 1
  TestHost.setForm ctx [ "Password", "secret" ]
  TestHost.run Handlers.loginSubmit ctx

  test <@ ctx.Response.StatusCode = 403 @>

[<Fact>]
let ``resetPassword sets member to unclaimed`` () =
  let hash = PasswordHashing.hash "original"
  let repo = repoWith []
  let ctx = TestHost.contextWithMembers repo [ claimedMember hash ]
  TestHost.setRouteId ctx 1
  TestHost.run Handlers.resetPassword ctx

  let memberRepo = ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()
  let saved = memberRepo.GetById(1) |> Option.get
  test <@ not (BpMonitor.Core.FamilyMember.isClaimed saved) @>

// ─── Trends handler tests ─────────────────────────────────────────────────────

// now = Tuesday 2026-06-09 12:00 UTC → ISO week 24, month June, year 2026
let private trendsNow = Timestamp.utc 2026 6 9 12 0 0

let private setRouteGran (ctx: Microsoft.AspNetCore.Http.HttpContext) (gran: string) =
  ctx.Request.RouteValues["gran"] <- box gran

let private setRouteGranKey (ctx: Microsoft.AspNetCore.Http.HttpContext) (gran: string) (key: string) =
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
  TestHost.run Handlers.trends ctx

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
  TestHost.run Handlers.trendsPanel ctx

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
  TestHost.run Handlers.trendsPanel ctx

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
  TestHost.run Handlers.trendsPanel ctx

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
  TestHost.run Handlers.trendsPanel ctx

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
  TestHost.run Handlers.trendsPanel ctx

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
  TestHost.run Handlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 200 @>
  let body = TestHost.readBody ctx
  test <@ body.Contains "No readings in" @>
  test <@ body.Contains "gran=weekly" |> not @>

[<Fact>]
let ``trendsPanel returns 400 for unrecognised gran`` () =
  let tp = FakeTimeProvider(trendsNow)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  // No route value → gran = None → 400
  TestHost.run Handlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 400 @>

[<Fact>]
let ``trendsPanel with invalid gran string returns 400`` () =
  let tp = FakeTimeProvider(trendsNow)
  let ctx = TestHost.contextWithProvider (repoWith []) tp
  setRouteGran ctx "notvalid"
  TestHost.run Handlers.trendsPanel ctx

  test <@ ctx.Response.StatusCode = 400 @>

// ─── Export handler tests ─────────────────────────────────────────────────────

[<Fact>]
let ``exportJson returns 200 with application/json content type`` () =
  let ctx = TestHost.context (repoWith [ sample ])
  TestHost.run Handlers.exportJson ctx

  test <@ ctx.Response.StatusCode = 200 @>
  test <@ ctx.Response.ContentType = "application/json; charset=utf-8" @>

[<Fact>]
let ``exportJson sets Content-Disposition to attachment with correct filename`` () =
  let ctx = TestHost.context (repoWith [ sample ])
  TestHost.run Handlers.exportJson ctx

  let disposition = ctx.Response.Headers["Content-Disposition"].ToString()
  test <@ disposition = "attachment; filename=\"bpmonitor-export.json\"" @>

[<Fact>]
let ``exportJson body contains the seeded reading's fields`` () =
  let ctx = TestHost.context (repoWith [ sample ])
  TestHost.run Handlers.exportJson ctx

  let body = TestHost.readBody ctx
  test <@ body.Contains "\"systolic\":120" @>
  test <@ body.Contains "\"diastolic\":80" @>
  test <@ body.Contains "\"heartRate\":66" @>

[<Fact>]
let ``exportJson returns only the active member's readings`` () =
  let otherMemberReading = { sample with Id = 2; MemberId = 999 }

  let repo = repoWith [ sample; otherMemberReading ]
  let ctx = TestHost.context repo
  TestHost.run Handlers.exportJson ctx

  let body = TestHost.readBody ctx
  // Only one object in the array — the repo filters by memberId
  test <@ body.StartsWith "[{" && body.EndsWith "}]" @>
