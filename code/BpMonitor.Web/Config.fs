namespace BpMonitor.Web

open System
open Microsoft.Extensions.Configuration
open BpMonitor.Core

/// Configuration helpers shared by the web host. Mirrors the range-reading
/// logic in the TUI so both UIs validate against the same bounds.
module Config =
  let readRanges (config: IConfiguration) =
    let s = config.GetSection("ReadingRanges")
    let d = ReadingRanges.defaults

    let getInt key fallback =
      match s[key] with
      | null -> fallback
      | v ->
        match Int32.TryParse(v) with
        | true, n -> n
        | _ -> fallback

    { SystolicMin = getInt "SystolicMin" d.SystolicMin
      SystolicMax = getInt "SystolicMax" d.SystolicMax
      DiastolicMin = getInt "DiastolicMin" d.DiastolicMin
      DiastolicMax = getInt "DiastolicMax" d.DiastolicMax
      HeartRateMin = getInt "HeartRateMin" d.HeartRateMin
      HeartRateMax = getInt "HeartRateMax" d.HeartRateMax }

  /// Human-readable validation messages, matching the wording the TUI uses
  /// so both UIs surface range errors consistently.
  let formatValidationErrors (ranges: ReadingRanges) (errors: ValidationError list) =
    errors
    |> List.map (fun e ->
      match e with
      | SystolicOutOfRange v -> $"Systolic {v} is out of range ({ranges.SystolicMin}–{ranges.SystolicMax})"
      | DiastolicOutOfRange v -> $"Diastolic {v} is out of range ({ranges.DiastolicMin}–{ranges.DiastolicMax})"
      | HeartRateOutOfRange v -> $"Heart rate {v} is out of range ({ranges.HeartRateMin}–{ranges.HeartRateMax})")
