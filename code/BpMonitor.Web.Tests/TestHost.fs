module TestHost

open System.Collections.Generic
open System.IO
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Primitives
open BpMonitor.Core

/// Builds a DefaultHttpContext wired with the given repository (and default
/// ranges) so the real Falco handlers can be invoked directly in tests.
let context (repo: IReadingRepository) : HttpContext =
  let services = ServiceCollection()
  services.AddLogging() |> ignore
  services.AddSingleton<IReadingRepository>(repo) |> ignore
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
