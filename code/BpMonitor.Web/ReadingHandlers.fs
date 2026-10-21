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
  // ── Form helpers ──

  /// Renders the add/edit form after a failed submit (status 422).
  let private renderFormErrors (ctx: HttpContext) s m page errors model : Task =
    ctx.Response.StatusCode <- 422
    htmlResponse (ReadingViews.readingForm s m page errors model) ctx

  /// The period `key` names, if it's in the period strip (a stale bookmark's isn't).
  let private visiblePeriod gran (key: string) now =
    TrendPeriod.available gran now |> List.tryFind (fun p -> p.Key = key)

  /// What a full-Trends query selects (`param` looks up one query parameter): its
  /// granularity (default Weekly) and, if its key is valid, the period.
  let private trendsSelection (param: string -> string option) (now: System.DateTimeOffset) =
    let gran =
      param Routes.trendsGranParam
      |> Option.bind TrendPeriod.parseGranularity
      |> Option.defaultValue Weekly

    gran,
    param Routes.trendsKeyParam
    |> Option.bind (fun key -> visiblePeriod gran key now)

  /// Allowlists where a reading edit may return to, so the param can't redirect off-site.
  /// A Trends query returns to the canonical URL of the period that page would show.
  let private returnTarget (now: System.DateTimeOffset) (page: string) =
    if List.contains page Routes.readingReturnTargets then
      page
    elif page.StartsWith(Routes.trends + "?", System.StringComparison.Ordinal) then
      let query =
        Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(page.Substring Routes.trends.Length)

      let param (key: string) =
        match query.TryGetValue key with
        | true, v -> Some(v.ToString())
        | _ -> None

      match trendsSelection param now with
      | gran, Some period -> Routes.trendsPeriod gran period.Key
      | _, None -> Routes.trends
    else
      Routes.history

  /// Validates a submitted form and persists via `save`; on any error re-renders
  /// the form with messages. Shared by create and update.
  let private submit
    (ctx: HttpContext)
    (s: LocalizedStrings)
    (m: FamilyMember)
    (pageFor: Binding.FormModel -> ReadingViews.ReadingFormPage)
    (save: BloodPressureReading -> unit)
    : Task =
    task {
      let log = logger ctx
      let! model = formModel ctx
      let page = pageFor model
      let rerender = renderFormErrors ctx s m page
      let rg = ranges ctx

      match Binding.toUnvalidated s model with
      | Error errorMessages ->
        log.LogWarning("Reading form validation failed (binding): {Errors}", errorMessages)
        do! rerender errorMessages model
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

          ctx.Response.Redirect(page.ReturnTo |> Option.defaultValue Routes.recent)
        | Error errors ->
          let messages = Config.formatValidationErrors s rg errors
          log.LogWarning("Reading form validation failed (domain): {Errors}", messages)
          do! rerender messages model
    }
    :> Task

  // ── App pages ──

  let landing: HttpContext -> Task =
    withMember (fun m ctx -> htmlResponse (ReadingViews.landing (stringsForMember m) m) ctx)

  // ── Medications Timeline (Wegier et al. 2021 Fig. 5): shared by /history and /recent ──

  let private toLocalDateOnly (ts: System.DateTimeOffset) : System.DateOnly =
    System.DateOnly.FromDateTime(ts.ToLocalTime().Date)

  /// Renders the Medications Timeline panel, opening on `rangeLow`–`rangeHigh`. The caller
  /// picks `medications` to match the BP chart's loaded data, which can reach beyond that
  /// visible range (panning). `showScrubber` mirrors the BP chart above it. Empty renders nothing.
  let private medicationsPanel
    (s: LocalizedStrings)
    (medications: Medication list)
    (showScrubber: bool)
    (rangeLow: System.DateTimeOffset)
    (rangeHigh: System.DateTimeOffset)
    : Falco.Markup.XmlNode =
    let chartHtml =
      BpChart.toHtmlMedications
        s.Charts
        showScrubber
        (Formats.formatLocal rangeLow)
        (Formats.formatLocal rangeHigh)
        medications

    MedicationViews.timelinePanel s chartHtml

  /// Readings-only would drop medications outside their span; union in the medications'
  /// own span. Readings count with their actual time, medication dates as local midnight
  /// (with that date's own UTC offset), and an ongoing medication (EndDate = None) as now.
  let private medicationsSpan
    (tp: System.TimeProvider)
    (readings: BloodPressureReading list)
    (medications: Medication list)
    : System.DateTimeOffset * System.DateTimeOffset =
    let now = tp.GetUtcNow()

    let localMidnight (d: System.DateOnly) =
      let local = d.ToDateTime(System.TimeOnly.MinValue)
      System.DateTimeOffset(local, System.TimeZoneInfo.Local.GetUtcOffset local)

    let points =
      (readings |> List.map _.Timestamp)
      @ (medications
         |> List.collect (fun med ->
           [
             localMidnight med.StartDate
             med.EndDate |> Option.map localMidnight |> Option.defaultValue now
           ]))

    match points with
    | [] -> now, now
    | ps -> List.min ps, List.max ps

  let history: HttpContext -> Task =
    withMember (fun m ctx ->
      let s = stringsForMember m
      let readings = sortedReadings m.Id ctx
      let chartHtml = BpChart.toHtml s.Charts m.Goal readings
      let medications = (medicationRepo ctx).GetAll(m.Id)
      let rangeLow, rangeHigh = medicationsSpan (timeProvider ctx) readings medications
      let panel = medicationsPanel s medications false rangeLow rangeHigh
      htmlResponse (ReadingViews.history s m chartHtml readings panel) ctx)

  let private recentChartWindowDays = 30

  // A year balances panning range against the LOWESS trend line's O(n^2) precompute cost.
  let private recentLoadWindowDays = 365

  // Shortcut buttons rendered above the chart; adding a new shortcut only means adding
  // an entry here.
  let private recentZoomShortcutDays (s: LocalizedStrings) =
    [ s.Reading.Last7Days, 7.0; s.Reading.Last30Days, float recentChartWindowDays ]

  // Shared by `recent` and `recentFull`: the chart always opens focused on the last
  // `recentChartWindowDays`, regardless of how much history is loaded behind it.
  let private renderRecentChart
    (s: LocalizedStrings)
    (m: FamilyMember)
    (now: System.DateTimeOffset)
    (readings: BloodPressureReading list)
    =
    let windowStart = now.AddDays(-float recentChartWindowDays)
    windowStart, BpChart.toHtmlRecent s.Charts m.Goal recentChartWindowDays windowStart now readings

  let recent: HttpContext -> Task =
    withMember (fun m ctx ->
      let s = stringsForMember m
      let now = (timeProvider ctx).GetUtcNow()
      let allReadings = (repo ctx).GetAll(m.Id)
      let loadWindowStart = now.AddDays(-float recentLoadWindowDays)

      let loadedReadings =
        allReadings
        |> ReadingStats.between loadWindowStart now
        |> List.sortByDescending _.Timestamp

      let hasOlderHistory =
        allReadings |> List.exists (fun r -> r.Timestamp < loadWindowStart)

      let windowStart, chartHtml = renderRecentChart s m now loadedReadings

      // Same period as the loaded readings, so panning back still shows the medications.
      let medications =
        (medicationRepo ctx).GetAll(m.Id)
        |> Medication.overlapping (toLocalDateOnly loadWindowStart) (toLocalDateOnly now)

      let panel =
        medicationsPanel s medications true windowStart (BpChart.recentAxisEnd windowStart now)

      htmlResponse
        (ReadingViews.recent
          s
          m
          chartHtml
          loadedReadings
          windowStart
          now
          (recentZoomShortcutDays s)
          hasOlderHistory
          panel)
        ctx)

  // "Load full history" target: htmx fragment re-rendering the container with all history.
  let recentFull: HttpContext -> Task =
    withMember (fun m ctx ->
      let s = stringsForMember m
      let now = (timeProvider ctx).GetUtcNow()

      // Excludes future-dated readings (clock skew, or a manually entered future
      // timestamp), same as `recent`'s load-window filter does for its bounded window.
      let allReadings =
        (repo ctx).GetAll(m.Id)
        |> List.filter (fun r -> r.Timestamp < now)
        |> List.sortByDescending _.Timestamp

      let windowStart, chartHtml = renderRecentChart s m now allReadings

      let medications =
        (medicationRepo ctx).GetAll(m.Id)
        |> Medication.overlapping System.DateOnly.MinValue (toLocalDateOnly now)

      let panel =
        medicationsPanel s medications true windowStart (BpChart.recentAxisEnd windowStart now)

      htmlResponse
        (ReadingViews.recentChartContainer
          s
          m
          chartHtml
          allReadings
          windowStart
          now
          (recentZoomShortcutDays s)
          false
          panel)
        ctx)

  let private renderTrendsData
    (s: LocalizedStrings)
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
      BpChart.toHtmlDashed s.Charts m.Goal gran (ReadingStats.aggregate gran windowed)

    summary, periods, periodsWithData, tableReadings, chartHtml

  /// The period `key` names, or the current one when it's missing or outside the strip.
  let private periodFor (gran: Granularity) (key: string option) (now: System.DateTimeOffset) =
    key
    |> Option.bind (fun k -> visiblePeriod gran k now)
    |> Option.defaultWith (fun () -> TrendPeriod.current gran now)

  /// The full page; `?gran=&key=` picks the panel, defaulting to Weekly/current.
  let trends: HttpContext -> Task =
    withMember (fun m ctx ->
      let s = stringsForMember m
      let now = (timeProvider ctx).GetUtcNow()
      let allReadings = (repo ctx).GetAll(m.Id)

      let gran, selected = trendsSelection (queryStr ctx) now
      let period = selected |> Option.defaultWith (fun () -> TrendPeriod.current gran now)

      let summary, periods, periodsWithData, tableReadings, chartHtml =
        renderTrendsData s gran period now m allReadings

      htmlResponse (TrendViews.trends s m summary periods periodsWithData tableReadings chartHtml) ctx)

  let trendsPanel: HttpContext -> Task =
    withMember (fun m ctx ->
      match routeStr ctx "gran" |> Option.bind TrendPeriod.parseGranularity with
      | None -> badRequest ctx
      | Some gran ->
        let s = stringsForMember m
        let now = (timeProvider ctx).GetUtcNow()
        let allReadings = (repo ctx).GetAll(m.Id)

        let period = periodFor gran (routeStr ctx "key") now

        let summary, periods, periodsWithData, tableReadings, chartHtml =
          renderTrendsData s gran period now m allReadings

        htmlResponse (TrendViews.trendsPanel s summary periods periodsWithData tableReadings chartHtml) ctx)

  // ── Settings: self-service goal range ──

  let settings: HttpContext -> Task =
    withMember (fun m ctx ->
      let s = stringsForMember m
      let medications = (medicationRepo ctx).GetAll(m.Id)

      htmlResponse
        (SettingsViews.settings
          s
          m.Name
          m.IsAdmin
          m.Language
          []
          {
            Binding.SysMin = string m.Goal.SystolicMin
            Binding.SysMax = string m.Goal.SystolicMax
            Binding.DiaMin = string m.Goal.DiastolicMin
            Binding.DiaMax = string m.Goal.DiastolicMax
          }
          medications
          [])
        ctx)

  /// Persists the member's chosen UI language and refreshes the login-page cookie.
  let updateLanguage: HttpContext -> Task =
    withMember (fun m ctx ->
      task {
        let! form = ctx.Request.ReadFormAsync()

        let lang =
          Language.tryParse (form[FormFields.language].ToString())
          |> Option.defaultValue m.Language

        (memberRepo ctx).UpdateLanguage m.Id lang
        setLanguageCookie ctx lang
        ctx.Response.Redirect Routes.settings
      }
      :> Task)

  let updateSettings: HttpContext -> Task =
    withMember (fun m ctx ->
      task {
        let s = stringsForMember m
        let! form = ctx.Request.ReadFormAsync()
        let raw key = form[key].ToString()

        let sysMinRaw, sysMaxRaw, diaMinRaw, diaMaxRaw =
          raw FormFields.systolicGoalMin,
          raw FormFields.systolicGoalMax,
          raw FormFields.diastolicGoalMin,
          raw FormFields.diastolicGoalMax

        let renderErrors errors =
          ctx.Response.StatusCode <- 422
          let medications = (medicationRepo ctx).GetAll(m.Id)

          htmlResponse
            (SettingsViews.settings
              s
              m.Name
              m.IsAdmin
              m.Language
              errors
              {
                Binding.SysMin = sysMinRaw
                Binding.SysMax = sysMaxRaw
                Binding.DiaMin = diaMinRaw
                Binding.DiaMax = diaMaxRaw
              }
              medications
              [])
            ctx

        // Parse-level errors accumulate across all four fields (Binding.tryInt is the
        // same parser used for the reading form), mirroring Binding.toUnvalidated.
        let parsed =
          validation {
            let! sysMin = Binding.tryInt s s.Member.SystolicMin sysMinRaw |> Validation.ofResult
            and! sysMax = Binding.tryInt s s.Member.SystolicMax sysMaxRaw |> Validation.ofResult
            and! diaMin = Binding.tryInt s s.Member.DiastolicMin diaMinRaw |> Validation.ofResult
            and! diaMax = Binding.tryInt s s.Member.DiastolicMax diaMaxRaw |> Validation.ofResult
            return sysMin, sysMax, diaMin, diaMax
          }

        match parsed with
        | Error parseErrors -> do! renderErrors parseErrors
        | Ok(sysMin, sysMax, diaMin, diaMax) ->
          match GoalRange.create sysMin sysMax diaMin diaMax with
          | Ok goal ->
            (memberRepo ctx).UpdateGoal m.Id goal
            ctx.Response.Redirect Routes.history
          | Error SystolicRangeInvalid -> do! renderErrors [ s.Errors.SystolicMinMustBeLessThanMax ]
          | Error DiastolicRangeInvalid -> do! renderErrors [ s.Errors.DiastolicMinMustBeLessThanMax ]
      }
      :> Task)

  // ── Reading CRUD ──

  let newReading: HttpContext -> Task =
    withMember (fun m ctx ->
      let s = stringsForMember m

      let prefill =
        { Binding.empty with
            Binding.Timestamp =
              (timeProvider ctx)
                .GetLocalNow()
                .ToString(Formats.timestamp, System.Globalization.CultureInfo.InvariantCulture)
        }

      htmlResponse (ReadingViews.readingForm s m (ReadingViews.addPage s) [] prefill) ctx)

  let createReading: HttpContext -> Task =
    withMember (fun m ctx ->
      let s = stringsForMember m

      submit ctx s m (fun _ -> ReadingViews.addPage s) ((repo ctx).Add m.Id))

  let editReading: HttpContext -> Task =
    withOwnedRecord "editReading" (fun ctx -> (repo ctx).GetAll) _.Id (fun m r ctx ->
      let s = stringsForMember m

      let returnTo =
        queryStr ctx FormFields.returnTo
        |> Option.defaultValue ""
        |> returnTarget ((timeProvider ctx).GetUtcNow())

      htmlResponse (ReadingViews.readingForm s m (ReadingViews.editPage s r.Id returnTo) [] (Binding.ofReading r)) ctx)

  let updateReading: HttpContext -> Task =
    withOwnedRecord "updateReading" (fun ctx -> (repo ctx).GetAll) _.Id (fun m existing ctx ->
      let s = stringsForMember m
      let now = (timeProvider ctx).GetUtcNow()

      submit ctx s m (_.ReturnTo >> returnTarget now >> ReadingViews.editPage s existing.Id) (fun r ->
        (repo ctx).Update
          { r with
              Id = existing.Id
              MemberId = m.Id
              CreatedAt = existing.CreatedAt
          }))

  // ── Export ──

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
