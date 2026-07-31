namespace BpMonitor.Web

open System
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Data

/// DI accessors, response primitives, route helpers, and form parsing shared by all handler modules.
module HandlerHelpers =
  let repo (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<IReadingRepository>()

  let memberRepo (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<IFamilyMemberRepository>()

  let dbContext (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<BpMonitorDbContext>()

  let ranges (ctx: HttpContext) =
    Config.readRanges (ctx.RequestServices.GetRequiredService<IConfiguration>())

  let timeProvider (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<TimeProvider>()

  let logger (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BpMonitor.Web.Handlers")

  let htmlResponse (node: XmlNode) (ctx: HttpContext) : Task =
    ctx.Response.ContentType <- "text/html; charset=utf-8"
    ctx.Response.WriteAsync(renderHtml node)

  let private jsonOptions =
    JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

  let jsonResponse (status: int) (payload: obj) (ctx: HttpContext) : Task =
    ctx.Response.StatusCode <- status
    ctx.Response.ContentType <- "application/json; charset=utf-8"
    ctx.Response.WriteAsync(JsonSerializer.Serialize(payload, jsonOptions))

  let badRequest (ctx: HttpContext) : Task =
    ctx.Response.StatusCode <- 400
    ctx.Response.WriteAsync("Bad request")

  let notFound (ctx: HttpContext) : Task =
    ctx.Response.StatusCode <- 404
    ctx.Response.WriteAsync("Not found")

  let forbidden (ctx: HttpContext) : Task =
    ctx.Response.StatusCode <- 403
    ctx.Response.WriteAsync("Forbidden")

  let routeInt (ctx: HttpContext) (key: string) : int option =
    match ctx.Request.RouteValues.TryGetValue key with
    | true, v ->
      match Int32.TryParse(string v) with
      | true, n -> Some n
      | _ -> None
    | _ -> None

  let routeStr (ctx: HttpContext) (key: string) : string option =
    match ctx.Request.RouteValues.TryGetValue key with
    | true, v ->
      let s = string v

      if String.IsNullOrEmpty s then None else Some s
    | _ -> None

  let formModel (ctx: HttpContext) : Task<Binding.FormModel> =
    task {
      let! form = ctx.Request.ReadFormAsync()

      let get (k: string) =
        match form.TryGetValue k with
        | true, v -> v.ToString()
        | _ -> ""

      return
        { Binding.Systolic = get FormFields.systolic
          Binding.Diastolic = get FormFields.diastolic
          Binding.HeartRate = get FormFields.heartRate
          Binding.Timestamp = get FormFields.timestamp
          Binding.Comments = get FormFields.comments }
    }

  let sortedReadings (memberId: int) (ctx: HttpContext) =
    (repo ctx).GetAll(memberId) |> List.sortByDescending _.Timestamp

  let private logBadRouteId (handlerName: string) (ctx: HttpContext) =
    (logger ctx)
      .LogWarning(
        "{Handler}: bad route value for {RouteId}",
        handlerName,
        routeStr ctx "id" |> Option.defaultValue "<missing>"
      )

  /// Matches an HttpContext whose "id" route segment parses as an int.
  let private (|RouteId|_|) (ctx: HttpContext) : int option = routeInt ctx "id"

  /// Resolves the "id" route segment to an int, logging and returning 400 for a noninteger id.
  let withRouteId (handlerName: string) (handler: int -> HttpContext -> Task) : HttpContext -> Task =
    fun ctx ->
      match ctx with
      | RouteId id -> handler id ctx
      | _ ->
        logBadRouteId handlerName ctx
        badRequest ctx

  /// Resolves the "id" route segment to a FamilyMember, logging and returning
  /// 400 for a noninteger id or 404 when no member with that id exists.
  let withRouteMember (handlerName: string) (handler: FamilyMember -> HttpContext -> Task) : HttpContext -> Task =
    fun ctx ->
      match ctx with
      | RouteId id ->
        match (memberRepo ctx).GetById(id) with
        | None ->
          (logger ctx).LogWarning("{Handler}: member {Id} not found", handlerName, id)
          notFound ctx
        | Some m -> handler m ctx
      | _ ->
        logBadRouteId handlerName ctx
        badRequest ctx
