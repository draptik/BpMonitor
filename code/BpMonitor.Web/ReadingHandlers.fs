namespace BpMonitor.Web

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open BpMonitor.Core
open BpMonitor.Charts
open BpMonitor.Export
open HandlerHelpers
open AuthHandlers

/// Handlers for reading CRUD, app pages (landing/history/trends/chart), and export.
module ReadingHandlers =
  // ---------------------------------------------------------------------------
  // Form helpers
  // ---------------------------------------------------------------------------

  /// Renders the add/edit form after a failed submit (status 422).
  let private renderFormErrors (ctx: HttpContext) active memberName isAdmin title action errors model : Task =
    ctx.Response.StatusCode <- 422
    htmlResponse (ReadingViews.readingForm active memberName isAdmin title action errors model) ctx

  /// Validates a submitted form and persists via `save`; on any error re-renders
  /// the form with messages. Shared by create and update.
  let private submit
    (ctx: HttpContext)
    active
    memberName
    isAdmin
    title
    action
    (save: BloodPressureReading -> unit)
    : Task =
    task {
      let log = logger ctx
      let! model = formModel ctx
      let rg = ranges ctx

      match Binding.toUnvalidated model with
      | Error errorMessages ->
        log.LogWarning("Reading form validation failed (binding): {Errors}", errorMessages)
        do! renderFormErrors ctx active memberName isAdmin title action errorMessages model
      | Ok unvalidated ->
        match BloodPressureReading.parse rg unvalidated with
        | Ok reading ->
          save reading

          log.LogInformation(
            "Saved reading — systolic={Systolic} diastolic={Diastolic} heartRate={HeartRate} timestamp={Timestamp}",
            reading.Systolic,
            reading.Diastolic,
            reading.HeartRate,
            reading.Timestamp
          )

          ctx.Response.Redirect Routes.history
        | Error errors ->
          let messages = Config.formatValidationErrors rg errors
          log.LogWarning("Reading form validation failed (domain): {Errors}", messages)
          do! renderFormErrors ctx active memberName isAdmin title action messages model
    }
    :> Task

  // ---------------------------------------------------------------------------
  // App pages
  // ---------------------------------------------------------------------------

  let landing: HttpContext -> Task =
    withMember (fun m ctx -> htmlResponse (ReadingViews.landing m) ctx)

  let history: HttpContext -> Task =
    withMember (fun m ctx ->
      let readings = sortedReadings m.Id ctx
      let chartHtml = BpChart.toHtml readings
      htmlResponse (ReadingViews.history m chartHtml readings) ctx)

  let trends: HttpContext -> Task =
    withMember (fun m ctx ->
      let now = (timeProvider ctx).GetUtcNow()
      let allReadings = (repo ctx).GetAll(m.Id)
      let period = TrendPeriod.current Weekly now
      let windowed = allReadings |> ReadingStats.between period.Start period.EndExclusive
      let summary = ReadingStats.summarizeRange period windowed
      let periods = TrendPeriod.available Weekly now
      let tableReadings = windowed |> List.sortByDescending _.Timestamp
      let chartHtml = BpChart.toHtmlDashed Weekly (ReadingStats.aggregate Weekly windowed)
      htmlResponse (TrendViews.trends m summary periods tableReadings chartHtml) ctx)

  let trendsPanel: HttpContext -> Task =
    withMember (fun m ctx ->
      match routeStr ctx "gran" |> Option.bind TrendPeriod.parseGranularity with
      | None -> badRequest ctx
      | Some gran ->
        let now = (timeProvider ctx).GetUtcNow()
        let allReadings = (repo ctx).GetAll(m.Id)

        let period =
          routeStr ctx "key"
          |> Option.bind (fun k -> TrendPeriod.ofKey gran k now)
          |> Option.defaultWith (fun () -> TrendPeriod.current gran now)

        let windowed = allReadings |> ReadingStats.between period.Start period.EndExclusive
        let summary = ReadingStats.summarizeRange period windowed
        let periods = TrendPeriod.available gran now
        let tableReadings = windowed |> List.sortByDescending _.Timestamp
        let chartHtml = BpChart.toHtmlDashed gran (ReadingStats.aggregate gran windowed)

        htmlResponse (TrendViews.trendsPanel summary periods tableReadings chartHtml) ctx)

  // ---------------------------------------------------------------------------
  // Reading CRUD
  // ---------------------------------------------------------------------------

  let newReading: HttpContext -> Task =
    fun ctx ->
      let m = authenticatedMember ctx

      let prefill =
        { Binding.empty with
            Binding.Timestamp = (timeProvider ctx).GetLocalNow().ToString(Formats.timestamp) }

      htmlResponse
        (ReadingViews.readingForm
          Routes.add
          (m |> Option.map _.Name |> Option.defaultValue "")
          (m |> Option.exists _.IsAdmin)
          "Add reading"
          Routes.readings
          []
          prefill)
        ctx

  let createReading: HttpContext -> Task =
    withMember (fun m ctx -> submit ctx Routes.add m.Name m.IsAdmin "Add reading" Routes.readings ((repo ctx).Add m.Id))

  let editReading: HttpContext -> Task =
    withMember (fun m ctx ->
      let log = logger ctx

      match routeInt ctx "id" with
      | None ->
        log.LogWarning(
          "editReading: bad route value for {RouteId}",
          routeStr ctx "id" |> Option.defaultValue "<missing>"
        )

        badRequest ctx
      | Some id ->
        match (repo ctx).GetAll(m.Id) |> List.tryFind (fun r -> r.Id = id) with
        | Some r ->
          htmlResponse
            (ReadingViews.readingForm "" m.Name m.IsAdmin "Edit reading" $"/readings/{id}" [] (Binding.ofReading r))
            ctx
        | None ->
          log.LogWarning("editReading: reading {Id} not found for member {MemberId}", id, m.Id)
          notFound ctx)

  let updateReading: HttpContext -> Task =
    withMember (fun m ctx ->
      let log = logger ctx

      match routeInt ctx "id" with
      | None ->
        log.LogWarning(
          "updateReading: bad route value for {RouteId}",
          routeStr ctx "id" |> Option.defaultValue "<missing>"
        )

        badRequest ctx
      | Some id ->
        submit ctx "" m.Name m.IsAdmin "Edit reading" $"/readings/{id}" (fun r ->
          (repo ctx).Update { r with Id = id; MemberId = m.Id }))

  // ---------------------------------------------------------------------------
  // Export
  // ---------------------------------------------------------------------------

  let exportJson: HttpContext -> Task =
    withMember (fun m ctx ->
      let json = JsonExport.serialize ((repo ctx).GetAll(m.Id))
      ctx.Response.ContentType <- "application/json; charset=utf-8"
      ctx.Response.Headers.Append("Content-Disposition", "attachment; filename=\"bpmonitor-export.json\"")
      ctx.Response.WriteAsync json)

  let exportCsv: HttpContext -> Task =
    withMember (fun m ctx ->
      let csv = CsvExport.serialize ((repo ctx).GetAll(m.Id))
      ctx.Response.ContentType <- "text/csv; charset=utf-8"
      ctx.Response.Headers.Append("Content-Disposition", "attachment; filename=\"bpmonitor-export.csv\"")
      ctx.Response.WriteAsync csv)
