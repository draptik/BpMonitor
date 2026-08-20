namespace BpMonitor.Web

open System
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

  let private tryDate (label: string) (s: string) : Result<DateOnly, string> =
    match DateOnly.TryParse(s) with
    | true, v -> Ok v
    | _ -> Error $"{label}: '{s}' is not a valid date"

  let private tryOptionalDate (label: string) (s: string) : Result<DateOnly option, string> =
    if String.IsNullOrWhiteSpace s then
      Ok None
    else
      tryDate label s |> Result.map Some

  let private toOption (s: string) =
    match s.Trim() with
    | "" -> None
    | s -> Some s

  /// Parse-level conversion (bad dates), mirroring Binding.toUnvalidated. Domain
  /// validation (empty name, end before start) happens afterward via Medication.parse.
  let private toUnvalidated (f: FormValues) : Validation<MedicationUnvalidated, string> =
    validation {
      let! startDate = tryDate "Start date" f.StartDate |> Validation.ofResult
      and! endDate = tryOptionalDate "End date" f.EndDate |> Validation.ofResult

      return
        { Name = f.Name
          FullName = toOption f.FullName
          Comment = toOption f.Comment
          StartDate = startDate
          EndDate = endDate }
    }

  let private medicationErrorMessage (error: MedicationError) =
    match error with
    | MedicationError.NameIsEmpty -> "Name cannot be empty"
    | MedicationError.EndDateBeforeStartDate -> "End date must be on or after the start date"

  /// Re-renders `/settings` (goal-range section unchanged) with the given medication
  /// errors, after a failed add.
  let private renderSettingsWithErrors (m: FamilyMember) (errors: string list) (ctx: HttpContext) : Task =
    ctx.Response.StatusCode <- 422
    let medications = (medicationRepo ctx).GetAll(m.Id)

    htmlResponse
      (SettingsViews.settings
        m.Name
        m.IsAdmin
        []
        (string m.Goal.SystolicMin)
        (string m.Goal.SystolicMax)
        (string m.Goal.DiastolicMin)
        (string m.Goal.DiastolicMax)
        medications
        errors)
      ctx

  let create: HttpContext -> Task =
    withMember (fun m ctx ->
      task {
        let! form = readForm ctx

        match toUnvalidated form with
        | Error errors -> do! renderSettingsWithErrors m errors ctx
        | Ok unvalidated ->
          match Medication.parse unvalidated with
          | Ok medication ->
            (medicationRepo ctx).Add m.Id medication
            ctx.Response.Redirect Routes.settings
          | Error errors -> do! renderSettingsWithErrors m (errors |> List.map medicationErrorMessage) ctx
      }
      :> Task)

  let edit: HttpContext -> Task =
    withMemberAndRouteId "editMedication" (fun m id ctx ->
      match (medicationRepo ctx).GetAll(m.Id) |> List.tryFind (fun x -> x.Id = id) with
      | None ->
        let log = logger ctx
        log.LogWarning("editMedication: medication {Id} not found for member {MemberId}", id, m.Id)
        notFound ctx
      | Some med ->
        htmlResponse
          (MedicationViews.medicationForm
            m.Name
            m.IsAdmin
            "Edit medication"
            (Routes.medicationUpdate id)
            []
            med.Name
            (med.FullName |> Option.defaultValue "")
            (med.Comment |> Option.defaultValue "")
            (Formats.formatDate med.StartDate)
            (med.EndDate |> Option.map Formats.formatDate |> Option.defaultValue ""))
          ctx)

  let private renderEditErrors
    (id: int)
    (m: FamilyMember)
    (errors: string list)
    (f: FormValues)
    (ctx: HttpContext)
    : Task =
    ctx.Response.StatusCode <- 422

    htmlResponse
      (MedicationViews.medicationForm
        m.Name
        m.IsAdmin
        "Edit medication"
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
        match (medicationRepo ctx).GetAll(m.Id) |> List.tryFind (fun x -> x.Id = id) with
        | None ->
          let log = logger ctx
          log.LogWarning("updateMedication: medication {Id} not found for member {MemberId}", id, m.Id)
          do! notFound ctx
        | Some existing ->
          let! form = readForm ctx

          match toUnvalidated form with
          | Error errors -> do! renderEditErrors id m errors form ctx
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
            | Error errors -> do! renderEditErrors id m (errors |> List.map medicationErrorMessage) form ctx
      }
      :> Task)

  let delete: HttpContext -> Task =
    withMemberAndRouteId "deleteMedication" (fun m id ctx ->
      (medicationRepo ctx).Delete m.Id id
      ctx.Response.Redirect Routes.settings
      Task.CompletedTask)
