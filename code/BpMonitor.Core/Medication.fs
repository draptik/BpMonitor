namespace BpMonitor.Core

open System

type MedicationUnvalidated =
  { Name: string
    FullName: string option
    Comment: string option
    StartDate: DateOnly
    EndDate: DateOnly option }

type Medication =
  { Id: int
    MemberId: int
    Name: string
    FullName: string option
    Comment: string option
    StartDate: DateOnly
    EndDate: DateOnly option
    CreatedAt: DateTimeOffset
    ModifiedAt: DateTimeOffset }

type MedicationError =
  | NameIsEmpty
  | EndDateBeforeStartDate

module Medication =
  open FsToolkit.ErrorHandling

  let private validateName (input: MedicationUnvalidated) : Validation<string, MedicationError> =
    if String.IsNullOrWhiteSpace input.Name then
      Validation.error NameIsEmpty
    else
      Validation.ok input.Name

  let private validateDates (input: MedicationUnvalidated) : Validation<unit, MedicationError> =
    match input.EndDate with
    | Some endDate when endDate < input.StartDate -> Validation.error EndDateBeforeStartDate
    | _ -> Validation.ok ()

  let parse (input: MedicationUnvalidated) : Validation<Medication, MedicationError> =
    validation {
      let! name = validateName input
      and! () = validateDates input

      return
        { Id = 0
          MemberId = 0
          Name = name
          FullName = input.FullName
          Comment = input.Comment
          StartDate = input.StartDate
          EndDate = input.EndDate
          CreatedAt = DateTimeOffset.MinValue
          ModifiedAt = DateTimeOffset.MinValue }
    }

  /// Medications whose [StartDate, EndDate] interval intersects [from, until]; ongoing
  /// medications (EndDate = None) count as running to `until` (and beyond).
  let overlapping (from: DateOnly) (until: DateOnly) (medications: Medication list) : Medication list =
    medications
    |> List.filter (fun m ->
      let startsInOrBeforeWindow = m.StartDate <= until

      let endsInOrAfterWindow =
        match m.EndDate with
        | None -> true
        | Some endDate -> endDate >= from

      startsInOrBeforeWindow && endsInOrAfterWindow)
