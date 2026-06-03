namespace BpMonitor.Web

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Charts

/// Falco HttpHandlers. Each resolves the per-request scoped repository from DI,
/// reuses Core validation, and renders Falco.Markup views.
module Handlers =
  let private repo (ctx: HttpContext) =
    ctx.RequestServices.GetRequiredService<IReadingRepository>()

  let private ranges (ctx: HttpContext) =
    Config.readRanges (ctx.RequestServices.GetRequiredService<IConfiguration>())

  let private htmlResponse (node: XmlNode) (ctx: HttpContext) : Task =
    ctx.Response.ContentType <- "text/html; charset=utf-8"
    ctx.Response.WriteAsync(renderHtml node)

  let private routeInt (ctx: HttpContext) (key: string) : int option =
    match ctx.Request.RouteValues.TryGetValue key with
    | true, v ->
      match Int32.TryParse(string v) with
      | true, n -> Some n
      | _ -> None
    | _ -> None

  let private formModel (ctx: HttpContext) : Task<Binding.FormModel> =
    task {
      let! form = ctx.Request.ReadFormAsync()

      let get (k: string) =
        match form.TryGetValue k with
        | true, v -> v.ToString()
        | _ -> ""

      return
        { Binding.Systolic = get "Systolic"
          Binding.Diastolic = get "Diastolic"
          Binding.HeartRate = get "HeartRate"
          Binding.Timestamp = get "Timestamp"
          Binding.Comments = get "Comments" }
    }

  let private sortedReadings (ctx: HttpContext) =
    (repo ctx).GetAll() |> List.sortByDescending _.Timestamp

  /// Renders the add/edit form after a failed submit (status 422).
  let private renderFormErrors (ctx: HttpContext) active title action errors model : Task =
    ctx.Response.StatusCode <- 422
    htmlResponse (Views.readingForm active title action errors model) ctx

  /// Validates a submitted form and persists via `save`; on any error re-renders
  /// the form with messages. Shared by create and update.
  let private submit (ctx: HttpContext) active title action (save: BloodPressureReading -> unit) : Task =
    task {
      let! model = formModel ctx
      let rg = ranges ctx

      match Binding.toUnvalidated model with
      | Error errorMessages -> do! renderFormErrors ctx active title action errorMessages model
      | Ok unvalidated ->
        match BloodPressureReading.parse rg unvalidated with
        | Ok reading ->
          save reading
          ctx.Response.Redirect "/history"
        | Error errors -> do! renderFormErrors ctx active title action (Config.formatValidationErrors rg errors) model
    }
    :> Task

  let landing: HttpContext -> Task = fun ctx -> htmlResponse Views.landing ctx

  let history: HttpContext -> Task =
    fun ctx -> htmlResponse (Views.history (sortedReadings ctx)) ctx

  let chart: HttpContext -> Task =
    fun ctx ->
      ctx.Response.ContentType <- "text/html; charset=utf-8"
      ctx.Response.WriteAsync(BpChart.toHtml ((repo ctx).GetAll()))

  let newReading: HttpContext -> Task =
    fun ctx ->
      let prefill =
        { Binding.empty with
            Binding.Timestamp = DateTimeOffset.Now.ToString(Formats.timestamp) }

      htmlResponse (Views.readingForm "/add" "Add reading" "/readings" [] prefill) ctx

  let createReading: HttpContext -> Task =
    fun ctx -> submit ctx "/add" "Add reading" "/readings" (repo ctx).Add

  let editReading: HttpContext -> Task =
    fun ctx ->
      match routeInt ctx "id" with
      | None ->
        ctx.Response.StatusCode <- 400
        ctx.Response.WriteAsync("Bad request")
      | Some id ->
        match (repo ctx).GetAll() |> List.tryFind (fun r -> r.Id = id) with
        | Some r -> htmlResponse (Views.readingForm "" "Edit reading" $"/readings/{id}" [] (Binding.ofReading r)) ctx
        | None ->
          ctx.Response.StatusCode <- 404
          ctx.Response.WriteAsync("Not found")

  let updateReading: HttpContext -> Task =
    fun ctx ->
      match routeInt ctx "id" with
      | None ->
        ctx.Response.StatusCode <- 400
        ctx.Response.WriteAsync("Bad request")
      | Some id -> submit ctx "" "Edit reading" $"/readings/{id}" (fun r -> (repo ctx).Update { r with Id = id })
