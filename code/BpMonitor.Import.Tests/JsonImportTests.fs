module BpMonitor.Import.Tests.JsonImportTests

open System
open BpMonitor.Data
open BpMonitor.Export
open BpMonitor.Core
open BpMonitor.Import.JsonImport
open Swensen.Unquote
open Xunit

let private makeReading id systolic diastolic heartRate (ts: DateTimeOffset) comments =
  { Id = id
    Systolic = systolic
    Diastolic = diastolic
    HeartRate = heartRate
    Timestamp = ts
    Comments = comments
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

[<Fact>]
let ``parse - valid JSON returns reading list`` () =
  let reading =
    makeReading 1 120 80 70 (DateTimeOffset(2024, 10, 15, 9, 0, 0, TimeSpan.Zero)) None

  let json = JsonExport.serialize [ reading ]
  let result = parse json
  test <@ result = Ok [ reading ] @>

[<Fact>]
let ``parse - empty array returns empty list`` () =
  let result = parse "[]"
  test <@ result = Ok [] @>

[<Fact>]
let ``parse - invalid JSON returns Error`` () =
  let result = parse "not valid json"

  test
    <@
      match result with
      | Error _ -> true
      | _ -> false
    @>

[<Fact>]
let ``import - new reading is added to repository`` () =
  let repo = InMemoryReadingRepository(Some []) :> IReadingRepository

  let reading =
    makeReading 99 120 80 70 (DateTimeOffset(2024, 10, 15, 9, 0, 0, TimeSpan.Zero)) None

  let result = import repo [ reading ]
  test <@ result.Added = 1 @>
  test <@ result.Updated = 0 @>

[<Fact>]
let ``import - existing reading with same timestamp is updated`` () =
  let ts = DateTimeOffset(2024, 10, 15, 9, 0, 0, TimeSpan.Zero)
  let existing = makeReading 1 110 70 60 ts None
  let repo = InMemoryReadingRepository(Some [ existing ]) :> IReadingRepository
  let updated = makeReading 99 120 80 70 ts None
  let result = import repo [ updated ]
  test <@ result.Added = 0 @>
  test <@ result.Updated = 1 @>
  test <@ repo.GetAll().[0].Systolic = 120 @>
  test <@ repo.GetAll().[0].Id = 1 @>

[<Fact>]
let ``import - empty list returns zero counts`` () =
  let repo = InMemoryReadingRepository(Some []) :> IReadingRepository
  let result = import repo []
  test <@ result.Added = 0 @>
  test <@ result.Updated = 0 @>
