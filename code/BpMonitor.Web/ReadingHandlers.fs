namespace BpMonitor.Web

open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open FsToolkit.ErrorHandling
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
    redirectTo
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

          ctx.Response.Redirect redirectTo
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
      let chartHtml = BpChart.toHtml m.Goal readings
      htmlResponse (ReadingViews.history m chartHtml readings) ctx)

  let private recentChartWindowDays = 30

  // Panning needs more than the 30-day focus window loaded, but truly all-time data makes
  // the LOWESS trend line's O(n^2) precompute (and the page payload) grow unboundedly with
  // account age. A year is generous for panning while keeping both bounded.
  let private recentLoadWindowDays = 365

  // Shortcut buttons rendered above the chart (ReadingViews.fs `zoomButtons`); adding a
  // new shortcut only means adding an entry here, not touching either function's signature.
  let private recentZoomShortcutDays =
    [ "Last 7 days", 7.0; "Last 30 days", float recentChartWindowDays ]

  // Shared by `recent` and `recentFull`: the chart always opens focused on the last
  // `recentChartWindowDays`, regardless of how much history is loaded behind it.
  let private renderRecentChart (m: FamilyMember) (now: System.DateTimeOffset) (readings: BloodPressureReading list) =
    let windowStart = now.AddDays(-float recentChartWindowDays)
    windowStart, BpChart.toHtmlRecent m.Goal recentChartWindowDays windowStart now readings

  let recent: HttpContext -> Task =
    withMember (fun m ctx ->
      let now = (timeProvider ctx).GetUtcNow()
      let allReadings = (repo ctx).GetAll(m.Id)
      let loadWindowStart = now.AddDays(-float recentLoadWindowDays)

      let loadedReadings =
        allReadings
        |> ReadingStats.between loadWindowStart now
        |> List.sortByDescending _.Timestamp

      let hasOlderHistory =
        allReadings |> List.exists (fun r -> r.Timestamp < loadWindowStart)

      let windowStart, chartHtml = renderRecentChart m now loadedReadings

      htmlResponse
        (ReadingViews.recent m chartHtml loadedReadings windowStart now recentZoomShortcutDays hasOlderHistory)
        ctx)

  // The "Load full history" button's target (ReadingViews.recentChartContainer): an
  // htmx fragment that re-renders the chart container with the member's *entire* history
  // loaded (still focused on the last 30 days), so panning works all the way back. Same
  // outerHTML-swap pattern as /trends' `trendsPanel`.
  let recentFull: HttpContext -> Task =
    withMember (fun m ctx ->
      let now = (timeProvider ctx).GetUtcNow()

      // Excludes future-dated readings (clock skew, or a manually entered future
      // timestamp), same as `recent`'s load-window filter does for its bounded window.
      let allReadings =
        (repo ctx).GetAll(m.Id)
        |> List.filter (fun r -> r.Timestamp < now)
        |> List.sortByDescending _.Timestamp

      let windowStart, chartHtml = renderRecentChart m now allReadings

      htmlResponse
        (ReadingViews.recentChartContainer m chartHtml allReadings windowStart now recentZoomShortcutDays false)
        ctx)

  let private renderTrendsData
    (gran: Granularity)
    (period: TrendPeriod)
    (now: System.DateTimeOffset)
    (m: FamilyMember)
    (allReadings: BloodPressureReading list)
    =
    let windowed = allReadings |> ReadingStats.between period.Start period.EndExclusive
    let summary = ReadingStats.summarizeRange period windowed
    let periods = TrendPeriod.available gran now

    let periodsWithData =
      periods
      |> List.filter (fun p ->
        allReadings
        |> ReadingStats.between p.Start p.EndExclusive
        |> List.isEmpty
        |> not)
      |> List.map _.Key
      |> Set.ofList

    let tableReadings = windowed |> List.sortByDescending _.Timestamp

    let chartHtml =
      BpChart.toHtmlDashed m.Goal gran (ReadingStats.aggregate gran windowed)

    summary, periods, periodsWithData, tableReadings, chartHtml

  let trends: HttpContext -> Task =
    withMember (fun m ctx ->
      let now = (timeProvider ctx).GetUtcNow()
      let allReadings = (repo ctx).GetAll(m.Id)
      let period = TrendPeriod.current Weekly now

      let summary, periods, periodsWithData, tableReadings, chartHtml =
        renderTrendsData Weekly period now m allReadings

      htmlResponse (TrendViews.trends m summary periods periodsWithData tableReadings chartHtml) ctx)

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

        let summary, periods, periodsWithData, tableReadings, chartHtml =
          renderTrendsData gran period now m allReadings

        htmlResponse (TrendViews.trendsPanel summary periods periodsWithData tableReadings chartHtml) ctx)

  // ---------------------------------------------------------------------------
  // Settings: self-service goal range
  // ---------------------------------------------------------------------------

  let settings: HttpContext -> Task =
    withMember (fun m ctx ->
      htmlResponse
        (MemberViews.settingsForm
          m.Name
          m.IsAdmin
          []
          (string m.Goal.SystolicMin)
          (string m.Goal.SystolicMax)
          (string m.Goal.DiastolicMin)
          (string m.Goal.DiastolicMax))
        ctx)

  let updateSettings: HttpContext -> Task =
    withMember (fun m ctx ->
      task {
        let! form = ctx.Request.ReadFormAsync()
        let raw key = form[key].ToString()

        let sysMinRaw, sysMaxRaw, diaMinRaw, diaMaxRaw =
          raw "SystolicGoalMin", raw "SystolicGoalMax", raw "DiastolicGoalMin", raw "DiastolicGoalMax"

        let renderErrors errors =
          ctx.Response.StatusCode <- 422
          htmlResponse (MemberViews.settingsForm m.Name m.IsAdmin errors sysMinRaw sysMaxRaw diaMinRaw diaMaxRaw) ctx

        // Parse-level errors accumulate across all four fields (Binding.tryInt is the
        // same parser used for the reading form), mirroring Binding.toUnvalidated.
        let parsed =
          validation {
            let! sysMin = Binding.tryInt "Systolic min" sysMinRaw |> Validation.ofResult
            and! sysMax = Binding.tryInt "Systolic max" sysMaxRaw |> Validation.ofResult
            and! diaMin = Binding.tryInt "Diastolic min" diaMinRaw |> Validation.ofResult
            and! diaMax = Binding.tryInt "Diastolic max" diaMaxRaw |> Validation.ofResult
            return sysMin, sysMax, diaMin, diaMax
          }

        match parsed with
        | Error parseErrors -> do! renderErrors parseErrors
        | Ok(sysMin, sysMax, diaMin, diaMax) ->
          match GoalRange.create sysMin sysMax diaMin diaMax with
          | Ok goal ->
            (memberRepo ctx).Update { m with Goal = goal }
            ctx.Response.Redirect Routes.history
          | Error SystolicRangeInvalid -> do! renderErrors [ "Systolic min must be less than systolic max" ]
          | Error DiastolicRangeInvalid -> do! renderErrors [ "Diastolic min must be less than diastolic max" ]
      }
      :> Task)

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
    withMember (fun m ctx ->
      submit ctx Routes.add m.Name m.IsAdmin "Add reading" Routes.readings Routes.recent ((repo ctx).Add m.Id))

  let editReading: HttpContext -> Task =
    withMember (fun m ctx ->
      (withRouteId "editReading" (fun id ctx ->
        match (repo ctx).GetAll(m.Id) |> List.tryFind (fun r -> r.Id = id) with
        | Some r ->
          htmlResponse
            (ReadingViews.readingForm "" m.Name m.IsAdmin "Edit reading" $"/readings/{id}" [] (Binding.ofReading r))
            ctx
        | None ->
          let log = logger ctx
          log.LogWarning("editReading: reading {Id} not found for member {MemberId}", id, m.Id)
          notFound ctx))
        ctx)

  let updateReading: HttpContext -> Task =
    withMember (fun m ctx ->
      (withRouteId "updateReading" (fun id ctx ->
        submit ctx "" m.Name m.IsAdmin "Edit reading" $"/readings/{id}" Routes.history (fun r ->
          (repo ctx).Update { r with Id = id; MemberId = m.Id })))
        ctx)

  // ---------------------------------------------------------------------------
  // Export
  // ---------------------------------------------------------------------------

  let private download (contentType: string) (filename: string) (body: string) (ctx: HttpContext) : Task =
    ctx.Response.ContentType <- contentType
    ctx.Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{filename}\"")
    ctx.Response.WriteAsync body

  let exportJson: HttpContext -> Task =
    withMember (fun m ctx ->
      download
        "application/json; charset=utf-8"
        "bpmonitor-export.json"
        (JsonExport.serialize ((repo ctx).GetAll(m.Id)))
        ctx)

  let exportCsv: HttpContext -> Task =
    withMember (fun m ctx ->
      download "text/csv; charset=utf-8" "bpmonitor-export.csv" (CsvExport.serialize ((repo ctx).GetAll(m.Id))) ctx)
