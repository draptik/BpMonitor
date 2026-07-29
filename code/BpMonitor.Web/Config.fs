namespace BpMonitor.Web

open System
open Microsoft.Extensions.Configuration
open BpMonitor.Core

/// Configuration helpers for the web host.
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

  /// Number of days a "remember me" login stays signed in for. Defaults to 30,
  /// clamped to 1..400 — 400 is the hard cap Firefox and Chrome both enforce on
  /// cookie lifetime, so anything above it would be silently truncated by the browser.
  let readRememberMeDays (config: IConfiguration) : int =
    let fallback = 30

    let parsed =
      match config["BpMonitor:RememberMeDays"] with
      | null -> fallback
      | v ->
        match Int32.TryParse(v) with
        | true, n -> n
        | _ -> fallback

    parsed |> max 1 |> min 400

  /// Human-readable validation messages for range errors.
  let formatValidationErrors (ranges: ReadingRanges) (errors: ValidationError list) =
    errors
    |> List.map (fun e ->
      match e with
      | SystolicOutOfRange v -> $"Systolic {v} is out of range ({ranges.SystolicMin}–{ranges.SystolicMax})"
      | DiastolicOutOfRange v -> $"Diastolic {v} is out of range ({ranges.DiastolicMin}–{ranges.DiastolicMax})"
      | HeartRateOutOfRange v -> $"Heart rate {v} is out of range ({ranges.HeartRateMin}–{ranges.HeartRateMax})")
