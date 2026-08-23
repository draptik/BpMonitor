namespace BpMonitor.Web

open System
open System.Globalization
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open FsToolkit.ErrorHandling
open BpMonitor.Core
open HandlerHelpers
open AuthHandlers

/// Handlers for medication CRUD (self-service, member-scoped — lives on `/settings`).
module MedicationHandlers =
  type private FormValues =
    { Name: string
      FullName: string
      Comment: string
      StartDate: string
      EndDate: string }

  let private readForm (ctx: HttpContext) : Task<FormValues> =
    task {
      let! form = ctx.Request.ReadFormAsync()

      let get (k: string) =
        match form.TryGetValue k with
        | true, v -> v.ToString()
        | _ -> ""

      return
        { Name = get FormFields.medicationName
          FullName = get FormFields.medicationFullName
          Comment = get FormFields.medicationComment
          StartDate = get FormFields.medicationStartDate
          EndDate = get FormFields.medicationEndDate }
    }

  /// "d.M.yyyy" accepts 1- or 2-digit day/month; yyyy-MM-dd is accepted too for pasted ISO dates.
  let private dateFormats = [| "d.M.yyyy"; Formats.date |]

  let private tryDate (s: LocalizedStrings) (label: string) (v: string) : Result<DateOnly, string> =
    match DateOnly.TryParseExact(v.Trim(), dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None) with
    | true, d -> Ok d
    | _ -> Error(s.Errors.NotAValidDate label v)

  let private tryOptionalDate (s: LocalizedStrings) (label: string) (v: string) : Result<DateOnly option, string> =
    if String.IsNullOrWhiteSpace v then
      Ok None
    else
      tryDate s label v |> Result.map Some

  /// Parse-level conversion (bad dates), mirroring Binding.toUnvalidated. Domain
  /// validation (empty name, end before start) happens afterward via Medication.parse.
  let private toUnvalidated (s: LocalizedStrings) (f: FormValues) : Validation<MedicationUnvalidated, string> =
    validation {
      let! startDate = tryDate s s.Medication.StartDateLabel f.StartDate |> Validation.ofResult
      and! endDate = tryOptionalDate s s.Medication.EndDateLabel f.EndDate |> Validation.ofResult

      return
        { Name = f.Name
          FullName = Binding.blankToOption f.FullName
          Comment = Binding.blankToOption f.Comment
          StartDate = startDate
          EndDate = endDate }
    }

  let private medicationErrorMessage (s: LocalizedStrings) (error: MedicationError) =
    match error with
    | MedicationError.NameIsEmpty -> s.Medication.NameIsEmpty
    | MedicationError.EndDateBeforeStartDate -> s.Medication.EndDateBeforeStartDate

  /// Re-renders `/settings` (goal-range section unchanged) with the given medication
  /// errors, after a failed add.
  let private renderSettingsWithErrors
    (s: LocalizedStrings)
    (m: FamilyMember)
    (errors: string list)
    (ctx: HttpContext)
    : Task =
    ctx.Response.StatusCode <- 422
    let medications = (medicationRepo ctx).GetAll(m.Id)

    htmlResponse
      (SettingsViews.settings
        s
        m.Name
        m.IsAdmin
        m.Language
        []
        { Binding.SysMin = string m.Goal.SystolicMin
          Binding.SysMax = string m.Goal.SystolicMax
          Binding.DiaMin = string m.Goal.DiastolicMin
          Binding.DiaMax = string m.Goal.DiastolicMax }
        medications
        errors)
      ctx

  let create: HttpContext -> Task =
    withMember (fun m ctx ->
      task {
        let s = stringsForMember m
        let! form = readForm ctx

        match toUnvalidated s form with
        | Error errors -> do! renderSettingsWithErrors s m errors ctx
        | Ok unvalidated ->
          match Medication.parse unvalidated with
          | Ok medication ->
            (medicationRepo ctx).Add m.Id medication
            ctx.Response.Redirect Routes.settings
          | Error errors -> do! renderSettingsWithErrors s m (errors |> List.map (medicationErrorMessage s)) ctx
      }
      :> Task)

  let edit: HttpContext -> Task =
    withMemberAndRouteId "editMedication" (fun m id ctx ->
      let s = stringsForMember m

      match (medicationRepo ctx).GetAll(m.Id) |> List.tryFind (fun x -> x.Id = id) with
      | None ->
        let log = logger ctx
        log.LogWarning("editMedication: medication {Id} not found for member {MemberId}", id, m.Id)
        notFound ctx
      | Some med ->
        htmlResponse
          (MedicationViews.medicationForm
            s
            m.Name
            m.IsAdmin
            s.Medication.EditMedicationTitle
            (Routes.medicationUpdate id)
            []
            med.Name
            (med.FullName |> Option.defaultValue "")
            (med.Comment |> Option.defaultValue "")
            (Formats.formatDateEuropean med.StartDate)
            (med.EndDate |> Option.map Formats.formatDateEuropean |> Option.defaultValue ""))
          ctx)

  let private renderEditErrors
    (s: LocalizedStrings)
    (id: int)
    (m: FamilyMember)
    (errors: string list)
    (f: FormValues)
    (ctx: HttpContext)
    : Task =
    ctx.Response.StatusCode <- 422

    htmlResponse
      (MedicationViews.medicationForm
        s
        m.Name
        m.IsAdmin
        s.Medication.EditMedicationTitle
        (Routes.medicationUpdate id)
        errors
        f.Name
        f.FullName
        f.Comment
        f.StartDate
        f.EndDate)
      ctx

  let update: HttpContext -> Task =
    withMemberAndRouteId "updateMedication" (fun m id ctx ->
      task {
        let s = stringsForMember m

        match (medicationRepo ctx).GetAll(m.Id) |> List.tryFind (fun x -> x.Id = id) with
        | None ->
          let log = logger ctx
          log.LogWarning("updateMedication: medication {Id} not found for member {MemberId}", id, m.Id)
          do! notFound ctx
        | Some existing ->
          let! form = readForm ctx

          match toUnvalidated s form with
          | Error errors -> do! renderEditErrors s id m errors form ctx
          | Ok unvalidated ->
            match Medication.parse unvalidated with
            | Ok medication ->
              (medicationRepo ctx)
                .Update(
                  { medication with
                      Id = id
                      MemberId = m.Id
                      CreatedAt = existing.CreatedAt }
                )

              ctx.Response.Redirect Routes.settings
            | Error errors -> do! renderEditErrors s id m (errors |> List.map (medicationErrorMessage s)) form ctx
      }
      :> Task)

  let delete: HttpContext -> Task =
    withMemberAndRouteId "deleteMedication" (fun m id ctx ->
      (medicationRepo ctx).Delete m.Id id
      ctx.Response.Redirect Routes.settings
      Task.CompletedTask)
