module TestHost

open System
open System.Collections.Generic
open System.IO
open System.Security.Claims
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Data

let private buildServices (repo: IReadingRepository) (memberRepo: IFamilyMemberRepository) (tp: TimeProvider) =
  let services = ServiceCollection()
  services.AddLogging() |> ignore
  services.AddSingleton<IReadingRepository>(repo) |> ignore
  services.AddSingleton<IFamilyMemberRepository>(memberRepo) |> ignore
  services.AddSingleton<IConfiguration>(ConfigurationBuilder().Build()) |> ignore
  services.AddSingleton<TimeProvider>(tp) |> ignore

  services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie()
  |> ignore

  services

/// Builds a ClaimsPrincipal for the given member, matching the scheme used by the web app.
let buildPrincipal (m: FamilyMember) : ClaimsPrincipal =
  let claims =
    [ yield Claim(ClaimTypes.NameIdentifier, string m.Id)
      yield Claim(ClaimTypes.Name, m.Name)
      if m.IsAdmin then
        yield Claim(ClaimTypes.Role, "Admin") ]

  let identity =
    ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)

  ClaimsPrincipal(identity)

let private defaultMember: FamilyMember =
  { Id = 1
    Name = "Me"
    IsAdmin = true
    IsActive = true
    PasswordHash = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

/// Builds a DefaultHttpContext wired with the given reading repository (and default
/// ranges + a single in-memory family member) so the real Falco handlers can be
/// invoked directly in tests. The default member has Id=1; the user principal is
/// pre-set so authenticatedMember resolves to member 1 without needing a DB.
let context (repo: IReadingRepository) : HttpContext =
  let memberRepo = InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository
  let ctx = DefaultHttpContext()
  ctx.RequestServices <- (buildServices repo memberRepo TimeProvider.System).BuildServiceProvider()
  ctx.Response.Body <- new MemoryStream()
  ctx.User <- buildPrincipal defaultMember
  ctx

/// Variant of `context` that injects a custom TimeProvider — useful for testing
/// handlers that read the current time (e.g. newReading timestamp prefill).
let contextWithProvider (repo: IReadingRepository) (tp: TimeProvider) : HttpContext =
  let memberRepo = InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository
  let ctx = DefaultHttpContext()
  ctx.RequestServices <- (buildServices repo memberRepo tp).BuildServiceProvider()
  ctx.Response.Body <- new MemoryStream()
  ctx.User <- buildPrincipal defaultMember
  ctx

/// Variant of `context` that uses a custom list of family members. The user
/// principal is set to the first member in the list. Useful for multi-member
/// scenarios (e.g. testing edit/update invariant enforcement).
let contextWithMembers (repo: IReadingRepository) (members: FamilyMember list) : HttpContext =
  let memberRepo =
    InMemoryFamilyMemberRepository(Some members) :> IFamilyMemberRepository

  let ctx = DefaultHttpContext()
  ctx.RequestServices <- (buildServices repo memberRepo TimeProvider.System).BuildServiceProvider()
  ctx.Response.Body <- new MemoryStream()

  match members with
  | first :: _ -> ctx.User <- buildPrincipal first
  | [] -> ()

  ctx

/// Variant of `context` that sets a specific authenticated user. Useful for
/// testing protected handlers with a particular member identity.
let contextWithUser (repo: IReadingRepository) (members: FamilyMember list) (loggedInMemberId: int) : HttpContext =
  let memberRepo =
    InMemoryFamilyMemberRepository(Some members) :> IFamilyMemberRepository

  let ctx = DefaultHttpContext()
  ctx.RequestServices <- (buildServices repo memberRepo TimeProvider.System).BuildServiceProvider()
  ctx.Response.Body <- new MemoryStream()

  match members |> List.tryFind (fun m -> m.Id = loggedInMemberId) with
  | Some m -> ctx.User <- buildPrincipal m
  | None -> ()

  ctx

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
