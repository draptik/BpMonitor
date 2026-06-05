module TestHost

open System.Collections.Generic
open System.IO
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Primitives
open BpMonitor.Core
open BpMonitor.Data

/// Builds a DefaultHttpContext wired with the given reading repository (and default
/// ranges + a single in-memory family member) so the real Falco handlers can be
/// invoked directly in tests. The default member has Id=1; the bp_member cookie
/// is pre-set so activeMember resolves to it without needing a DB.
let context (repo: IReadingRepository) : HttpContext =
  let memberRepo = InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository

  let services = ServiceCollection()
  services.AddLogging() |> ignore
  services.AddSingleton<IReadingRepository>(repo) |> ignore
  services.AddSingleton<IFamilyMemberRepository>(memberRepo) |> ignore
  services.AddSingleton<IConfiguration>(ConfigurationBuilder().Build()) |> ignore

  let ctx = DefaultHttpContext()
  ctx.RequestServices <- services.BuildServiceProvider()
  ctx.Response.Body <- new MemoryStream()
  // No cookie set: activeMember falls back to the first member (Id=1) from InMemoryFamilyMemberRepository.
  ctx

/// Variant of `context` that uses a custom list of family members. Useful for
/// multi-member scenarios (e.g. testing edit/update invariant enforcement).
let contextWithMembers (repo: IReadingRepository) (members: FamilyMember list) : HttpContext =
  let memberRepo =
    InMemoryFamilyMemberRepository(Some members) :> IFamilyMemberRepository

  let services = ServiceCollection()
  services.AddLogging() |> ignore
  services.AddSingleton<IReadingRepository>(repo) |> ignore
  services.AddSingleton<IFamilyMemberRepository>(memberRepo) |> ignore
  services.AddSingleton<IConfiguration>(ConfigurationBuilder().Build()) |> ignore

  let ctx = DefaultHttpContext()
  ctx.RequestServices <- services.BuildServiceProvider()
  ctx.Response.Body <- new MemoryStream()
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
