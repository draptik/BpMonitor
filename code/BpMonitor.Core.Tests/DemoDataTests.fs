module DemoDataTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core

let private ranges = ReadingRanges.defaults
let private now = DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero)

// ── member shape ─────────────────────────────────────────────────────────────

[<Fact>]
let ``simpsons produces exactly 5 member entries`` () =
  let result = DemoData.simpsons ranges now
  test <@ result.Length = 5 @>

[<Fact>]
let ``simpsons has exactly one admin member`` () =
  let result = DemoData.simpsons ranges now
  let admins = result |> List.filter (fun (spec, _) -> spec.IsAdmin)
  test <@ admins.Length = 1 @>

[<Fact>]
let ``simpsons admin is Marge Simpson`` () =
  let result = DemoData.simpsons ranges now
  let (spec, _) = result |> List.find (fun (spec, _) -> spec.IsAdmin)
  test <@ spec.Name = "Marge Simpson" @>

// ── reading validity ──────────────────────────────────────────────────────────

[<Fact>]
let ``all generated readings are in range (no parse errors)`` () =
  let result = DemoData.simpsons ranges now

  for (spec, readings) in result do
    for r in readings do
      test
        <@
          r.Systolic >= ranges.SystolicMin
          && r.Systolic <= ranges.SystolicMax
          && r.Diastolic >= ranges.DiastolicMin
          && r.Diastolic <= ranges.DiastolicMax
          && r.HeartRate >= ranges.HeartRateMin
          && r.HeartRate <= ranges.HeartRateMax
        @>

// ── timestamp span ────────────────────────────────────────────────────────────

[<Fact>]
let ``readings span roughly 5 years`` () =
  let result = DemoData.simpsons ranges now
  let allReadings = result |> List.collect snd
  let earliest = allReadings |> List.map _.Timestamp |> List.min
  let latest = allReadings |> List.map _.Timestamp |> List.max
  let spanDays = (latest - earliest).TotalDays
  // Expect at least 4 years of spread across all members
  test <@ spanDays >= 4.0 * 365.0 @>

[<Fact>]
let ``readings end at or before the anchor time`` () =
  let result = DemoData.simpsons ranges now
  let allReadings = result |> List.collect snd
  let latest = allReadings |> List.map _.Timestamp |> List.max
  test <@ latest <= now @>

// ── determinism ──────────────────────────────────────────────────────────────

[<Fact>]
let ``same anchor produces identical results (deterministic)`` () =
  let r1 = DemoData.simpsons ranges now
  let r2 = DemoData.simpsons ranges now
  let counts1 = r1 |> List.map (fun (s, rs) -> s.Name, rs.Length)
  let counts2 = r2 |> List.map (fun (s, rs) -> s.Name, rs.Length)
  test <@ counts1 = counts2 @>

// ── member profiles are distinct ──────────────────────────────────────────────

[<Fact>]
let ``Homer mean systolic is higher than Marge mean systolic`` () =
  let result = DemoData.simpsons ranges now

  let meanSys name =
    let (_, readings) = result |> List.find (fun (s, _) -> s.Name = name)
    readings |> List.averageBy (fun r -> float r.Systolic)

  test <@ meanSys "Homer Simpson" > meanSys "Marge Simpson" @>

[<Fact>]
let ``Marge mean systolic is higher than Bart mean systolic`` () =
  let result = DemoData.simpsons ranges now

  let meanSys name =
    let (_, readings) = result |> List.find (fun (s, _) -> s.Name = name)
    readings |> List.averageBy (fun r -> float r.Systolic)

  test <@ meanSys "Marge Simpson" > meanSys "Bart Simpson" @>

[<Fact>]
let ``each member has enough readings for trend granularities`` () =
  let result = DemoData.simpsons ranges now
  // Minimum: expect at least 52 readings per member (roughly 1 per week for a year)
  for (spec, readings) in result do
    test <@ readings.Length >= 52 @>
