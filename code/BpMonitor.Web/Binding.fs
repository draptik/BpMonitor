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

  /// Parses `s` as an int, or an `Errors.NotAnInteger` message.
  /// Shared by reading and goal-range form parsing.
  let tryInt (strings: LocalizedStrings) (label: string) (s: string) : Result<int, string> =
    match Int32.TryParse(s) with
    | true, v -> Ok v
    | _ -> Error(strings.Errors.NotAnInteger label s)

  let private tryTimestamp (strings: LocalizedStrings) (s: string) : Result<DateTimeOffset, string> =
    match DateTimeOffset.TryParse(s) with
    | true, v -> Ok v
    | _ -> Error(strings.Errors.NotAValidDateTime s)

  /// Blank string → None; otherwise `Some` of the trimmed value.
  let blankToOption (s: string) : string option =
    match s.Trim() with
    | "" -> None
    | s -> Some s

  /// Parse-level conversion. Returns the unvalidated reading or the list of
  /// parse errors (range checks happen afterward via BloodPressureReading.parse).
  let toUnvalidated (strings: LocalizedStrings) (m: FormModel) : Validation<BloodPressureReadingUnvalidated, string> =
    validation {
      let! sys = tryInt strings strings.Table.Systolic m.Systolic |> Validation.ofResult
      and! dia = tryInt strings strings.Table.Diastolic m.Diastolic |> Validation.ofResult
      and! hr = tryInt strings strings.Table.HeartRate m.HeartRate |> Validation.ofResult
      and! ts = tryTimestamp strings m.Timestamp |> Validation.ofResult

      let comments = blankToOption m.Comments

      return
        { Systolic = sys
          Diastolic = dia
          HeartRate = hr
          Timestamp = ts
          Comments = comments }
    }
