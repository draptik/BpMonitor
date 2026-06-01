namespace BpMonitor.Web

open System
open FsToolkit.ErrorHandling
open BpMonitor.Core

/// Maps raw form fields to a `BloodPressureReadingUnvalidated`, accumulating
/// parse-level errors (bad integers / timestamp). Range validation is then
/// delegated to `BloodPressureReading.parse`.
module Binding =
  type FormModel =
    { Systolic: string
      Diastolic: string
      HeartRate: string
      Timestamp: string
      Comments: string }

  let empty =
    { Systolic = ""
      Diastolic = ""
      HeartRate = ""
      Timestamp = ""
      Comments = "" }

  let ofReading (r: BloodPressureReading) =
    { Systolic = string r.Systolic
      Diastolic = string r.Diastolic
      HeartRate = string r.HeartRate
      Timestamp = Formats.formatLocal r.Timestamp
      Comments = r.Comments |> Option.defaultValue "" }

  let private tryInt (label: string) (s: string) : Result<int, string> =
    match Int32.TryParse(s) with
    | true, v -> Ok v
    | _ -> Error $"{label}: '{s}' is not a valid integer"

  let private tryTimestamp (s: string) : Result<DateTimeOffset, string> =
    match DateTimeOffset.TryParse(s) with
    | true, v -> Ok v
    | _ -> Error $"Timestamp: '{s}' is not a valid date/time"

  /// Parse-level conversion. Returns the unvalidated reading or the list of
  /// parse errors (range checks happen afterwards via BloodPressureReading.parse).
  let toUnvalidated (m: FormModel) : Validation<BloodPressureReadingUnvalidated, string> =
    validation {
      let! sys = tryInt "Systolic" m.Systolic |> Validation.ofResult
      and! dia = tryInt "Diastolic" m.Diastolic |> Validation.ofResult
      and! hr = tryInt "Heart Rate" m.HeartRate |> Validation.ofResult
      and! ts = tryTimestamp m.Timestamp |> Validation.ofResult

      let comments =
        match m.Comments.Trim() with
        | "" -> None
        | s -> Some s

      return
        { Systolic = sys
          Diastolic = dia
          HeartRate = hr
          Timestamp = ts
          Comments = comments }
    }
