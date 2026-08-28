module TestHost

open System
open System.Collections.Generic
open System.IO
open System.Security.Claims
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Primitives
open Microsoft.EntityFrameworkCore
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Web

let private buildServices
  (repo: IReadingRepository)
  (memberRepo: IFamilyMemberRepository)
  (medicationRepo: IMedicationRepository)
  (tp: TimeProvider)
  =
  let services = ServiceCollection()
  services.AddLogging() |> ignore
  services.AddSingleton<IReadingRepository>(repo) |> ignore
  services.AddSingleton<IFamilyMemberRepository>(memberRepo) |> ignore
  services.AddSingleton<IMedicationRepository>(medicationRepo) |> ignore
  services.AddSingleton<IConfiguration>(ConfigurationBuilder().Build()) |> ignore
  services.AddSingleton<TimeProvider>(tp) |> ignore

  services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie()
  |> ignore

  services

let buildPrincipal (m: FamilyMember) : ClaimsPrincipal = AuthHandlers.claimsPrincipal m

let private defaultMember: FamilyMember =
  { Id = 1
    Name = "Me"
    IsAdmin = true
    IsActive = true
    PasswordHash = None
    Goal = GoalRange.defaults
    Language = English
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let private newCtx (services: ServiceCollection) (user: ClaimsPrincipal option) : HttpContext =
  let ctx = DefaultHttpContext()
  ctx.RequestServices <- services.BuildServiceProvider()
  ctx.Response.Body <- new MemoryStream()

  match user with
  | Some u -> ctx.User <- u
  | None -> ()

  ctx

/// Builds a DefaultHttpContext wired with the given reading repository (and default
/// ranges + a single in-memory family member) so the real Falco handlers can be
/// invoked directly in tests. The default member has Id=1; the user principal is
/// pre-set so authenticatedMember resolves to member 1 without needing a DB.
let private emptyMedicationRepo () =
  InMemoryMedicationRepository(None) :> IMedicationRepository

let context (repo: IReadingRepository) : HttpContext =
  let memberRepo = InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository

  newCtx
    (buildServices repo memberRepo (emptyMedicationRepo ()) TimeProvider.System)
    (Some(buildPrincipal defaultMember))

/// Variant of `context` that injects a custom TimeProvider — useful for testing
/// handlers that read the current time (e.g., newReading timestamp prefill).
let contextWithProvider (repo: IReadingRepository) (tp: TimeProvider) : HttpContext =
  let memberRepo = InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository
  newCtx (buildServices repo memberRepo (emptyMedicationRepo ()) tp) (Some(buildPrincipal defaultMember))

/// Variant of `context` that uses a custom list of family members. The user
/// principal is set to be the first member in the list. Useful for multi-member
/// scenarios (e.g., testing edit/update invariant enforcement).
let contextWithMembers (repo: IReadingRepository) (members: FamilyMember list) : HttpContext =
  let memberRepo =
    InMemoryFamilyMemberRepository(Some members) :> IFamilyMemberRepository

  let user = members |> List.tryHead |> Option.map buildPrincipal
  newCtx (buildServices repo memberRepo (emptyMedicationRepo ()) TimeProvider.System) user

/// Variant of `contextWithMembers` that also injects a custom TimeProvider —
/// useful for testing handlers that need both a non-default member (e.g., a
/// custom goal range) and control over the current time (e.g., trends windows).
let contextWithMembersAndProvider
  (repo: IReadingRepository)
  (members: FamilyMember list)
  (tp: TimeProvider)
  : HttpContext =
  let memberRepo =
    InMemoryFamilyMemberRepository(Some members) :> IFamilyMemberRepository

  let user = members |> List.tryHead |> Option.map buildPrincipal
  newCtx (buildServices repo memberRepo (emptyMedicationRepo ()) tp) user

/// Variant of `context` with no signed-in user — for testing the `protect`/`protectAdmin`
/// auth combinators against an unauthenticated request.
let contextUnauthenticated (repo: IReadingRepository) : HttpContext =
  let memberRepo = InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository
  newCtx (buildServices repo memberRepo (emptyMedicationRepo ()) TimeProvider.System) None

/// Variant of `context` that sets a specific authenticated user. Useful for
/// testing protected handlers with a particular member identity.
let contextWithUser (repo: IReadingRepository) (members: FamilyMember list) (loggedInMemberId: int) : HttpContext =
  let memberRepo =
    InMemoryFamilyMemberRepository(Some members) :> IFamilyMemberRepository

  let user =
    members
    |> List.tryFind (fun m -> m.Id = loggedInMemberId)
    |> Option.map buildPrincipal

  newCtx (buildServices repo memberRepo (emptyMedicationRepo ()) TimeProvider.System) user

/// Variant of `context` that seeds the medication repository with a custom initial
/// list — for medication CRUD and Medications Timeline handler tests.
let contextWithMedications (repo: IReadingRepository) (medications: Medication list) : HttpContext =
  let memberRepo = InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository

  let medicationRepo =
    InMemoryMedicationRepository(Some medications) :> IMedicationRepository

  newCtx (buildServices repo memberRepo medicationRepo TimeProvider.System) (Some(buildPrincipal defaultMember))

/// Variant of `contextWithMedications` that also injects a custom TimeProvider —
/// useful for testing medicationsSpan's "ongoing medication runs to today" behavior.
let contextWithMedicationsAndProvider
  (repo: IReadingRepository)
  (medications: Medication list)
  (tp: TimeProvider)
  : HttpContext =
  let memberRepo = InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository

  let medicationRepo =
    InMemoryMedicationRepository(Some medications) :> IMedicationRepository

  newCtx (buildServices repo memberRepo medicationRepo tp) (Some(buildPrincipal defaultMember))

/// Builds a context wired with a real SQLite-backed BpMonitorDbContext, for the
/// health handler. Pass a temp-file connection string for the reachable case and
/// a path under a nonexistent directory for the unreachable case.
let healthContext (connectionString: string) : HttpContext =
  let services = ServiceCollection()
  services.AddLogging() |> ignore

  services.AddDbContext<BpMonitorDbContext>(fun o -> o.UseSqlite(connectionString) |> ignore)
  |> ignore

  newCtx services None

/// Reads back whatever a handler wrote to the response body.
let readBody (ctx: HttpContext) : string =
  ctx.Response.Body.Position <- 0L
  use reader = new StreamReader(ctx.Response.Body)
  reader.ReadToEnd()

/// Sets an urlencoded form on the request, so ReadFormAsync returns it.
let setForm (ctx: HttpContext) (pairs: (string * string) list) =
  ctx.Request.Method <- "POST"
  ctx.Request.ContentType <- "application/x-www-form-urlencoded"

  let dict =
    pairs |> List.map (fun (k, v) -> KeyValuePair(k, StringValues v)) |> Dictionary

  ctx.Request.Form <- FormCollection(dict)

let setRouteId (ctx: HttpContext) (id: int) =
  ctx.Request.RouteValues["id"] <- box (string id)

/// Runs a handler (HttpContext -> Task) to completion.
let run (handler: HttpContext -> System.Threading.Tasks.Task) (ctx: HttpContext) =
  (handler ctx).GetAwaiter().GetResult()
