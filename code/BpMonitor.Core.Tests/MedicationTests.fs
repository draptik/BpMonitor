module MedicationTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core

let private validUnvalidated: MedicationUnvalidated =
  { Name = "HCTZ"
    FullName = Some "hydrochlorothiazide"
    Comment = None
    StartDate = DateOnly(2026, 1, 1)
    EndDate = None }

[<Fact>]
let ``parse returns Ok when input is valid`` () =
  test <@ Medication.parse validUnvalidated |> Result.isOk @>

[<Fact>]
let ``parse returns Error when name is empty`` () =
  test <@ Medication.parse { validUnvalidated with Name = "" } |> Result.isError @>

[<Fact>]
let ``parse returns Error when name is whitespace only`` () =
  test <@ Medication.parse { validUnvalidated with Name = "   " } |> Result.isError @>

[<Fact>]
let ``parse returns Error when end date is before start date`` () =
  test
    <@
      Medication.parse
        { validUnvalidated with
            StartDate = DateOnly(2026, 1, 10)
            EndDate = Some(DateOnly(2026, 1, 1)) }
      |> Result.isError
    @>

[<Fact>]
let ``parse allows end date equal to start date`` () =
  test
    <@
      Medication.parse
        { validUnvalidated with
            StartDate = DateOnly(2026, 1, 1)
            EndDate = Some(DateOnly(2026, 1, 1)) }
      |> Result.isOk
    @>

[<Fact>]
let ``parse allows an absent FullName`` () =
  test
    <@
      Medication.parse
        { validUnvalidated with
            FullName = None }
      |> Result.isOk
    @>

[<Fact>]
let ``parse allows an absent Comment`` () =
  test <@ Medication.parse { validUnvalidated with Comment = None } |> Result.isOk @>

[<Fact>]
let ``parse collects all validation errors`` () =
  match
    Medication.parse
      { validUnvalidated with
          Name = ""
          StartDate = DateOnly(2026, 1, 10)
          EndDate = Some(DateOnly(2026, 1, 1)) }
  with
  | Error errors -> test <@ errors.Length = 2 @>
  | Ok _ -> failwith "Expected Error"

[<Fact>]
let ``parse sets MemberId to 0`` () =
  match Medication.parse validUnvalidated with
  | Ok m -> test <@ m.MemberId = 0 @>
  | Error _ -> failwith "Expected Ok"

[<Fact>]
let ``parse sets CreatedAt and ModifiedAt to MinValue`` () =
  match Medication.parse validUnvalidated with
  | Ok m ->
    test <@ m.CreatedAt = DateTimeOffset.MinValue @>
    test <@ m.ModifiedAt = DateTimeOffset.MinValue @>
  | Error _ -> failwith "Expected Ok"

// ── overlapping ─────────────────────────────────────────────────────────────

let private med id name (start: DateOnly) (endDate: DateOnly option) : Medication =
  { Id = id
    MemberId = 1
    Name = name
    FullName = None
    Comment = None
    StartDate = start
    EndDate = endDate
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

[<Fact>]
let ``overlapping includes an ongoing medication that started before the window`` () =
  let m = med 1 "HCTZ" (DateOnly(2026, 1, 1)) None

  let result =
    Medication.overlapping (DateOnly(2026, 2, 1)) (DateOnly(2026, 3, 1)) [ m ]

  test <@ result = [ m ] @>

[<Fact>]
let ``overlapping excludes a medication that ended before the window`` () =
  let m = med 1 "HCTZ" (DateOnly(2026, 1, 1)) (Some(DateOnly(2026, 1, 15)))

  let result =
    Medication.overlapping (DateOnly(2026, 2, 1)) (DateOnly(2026, 3, 1)) [ m ]

  test <@ result = [] @>

[<Fact>]
let ``overlapping excludes a medication that starts after the window`` () =
  let m = med 1 "HCTZ" (DateOnly(2026, 4, 1)) None

  let result =
    Medication.overlapping (DateOnly(2026, 2, 1)) (DateOnly(2026, 3, 1)) [ m ]

  test <@ result = [] @>

[<Fact>]
let ``overlapping includes a medication that partially overlaps the window start`` () =
  let m = med 1 "HCTZ" (DateOnly(2026, 1, 15)) (Some(DateOnly(2026, 2, 15)))

  let result =
    Medication.overlapping (DateOnly(2026, 2, 1)) (DateOnly(2026, 3, 1)) [ m ]

  test <@ result = [ m ] @>

[<Fact>]
let ``overlapping includes a medication whose end date equals the window start`` () =
  let m = med 1 "HCTZ" (DateOnly(2026, 1, 1)) (Some(DateOnly(2026, 2, 1)))

  let result =
    Medication.overlapping (DateOnly(2026, 2, 1)) (DateOnly(2026, 3, 1)) [ m ]

  test <@ result = [ m ] @>

[<Fact>]
let ``overlapping includes a medication whose start date equals the window end`` () =
  let m = med 1 "HCTZ" (DateOnly(2026, 3, 1)) None

  let result =
    Medication.overlapping (DateOnly(2026, 2, 1)) (DateOnly(2026, 3, 1)) [ m ]

  test <@ result = [ m ] @>

[<Fact>]
let ``overlapping filters a mixed list down to only the medications that overlap the window`` () =
  let ongoing = med 1 "HCTZ" (DateOnly(2026, 1, 1)) None

  let endedBefore =
    med 2 "Amlodipine" (DateOnly(2025, 1, 1)) (Some(DateOnly(2025, 6, 1)))

  let startsAfter = med 3 "Losartan" (DateOnly(2026, 4, 1)) None

  let withinWindow =
    med 4 "Lisinopril" (DateOnly(2026, 2, 10)) (Some(DateOnly(2026, 2, 20)))

  let result =
    Medication.overlapping
      (DateOnly(2026, 2, 1))
      (DateOnly(2026, 3, 1))
      [ ongoing; endedBefore; startsAfter; withinWindow ]

  test <@ result = [ ongoing; withinWindow ] @>
