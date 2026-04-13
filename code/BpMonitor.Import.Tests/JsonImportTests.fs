module BpMonitor.Import.Tests.JsonImportTests

open System
open System.IO
open BpMonitor.Data
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
  let json =
    """[{"id":1,"systolic":120,"diastolic":80,"heartRate":70,"timestamp":"2024-10-15T09:00:00+00:00","comments":null,"createdAt":"0001-01-01T00:00:00+00:00","modifiedAt":"0001-01-01T00:00:00+00:00"}]"""

  let result = parse json

  let expected = makeReading 1 120 80 70 (Timestamp.utc 2024 10 15 9 0 0) None

  test <@ result = Ok [ expected ] @>

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
let ``tryReadFromFile returns Ok with readings when file contains valid JSON`` () =
  let path = Path.GetTempFileName()

  File.WriteAllText(
    path,
    """[{"id":1,"systolic":120,"diastolic":80,"heartRate":70,"timestamp":"2024-10-15T09:00:00+00:00","comments":"morning","createdAt":"0001-01-01T00:00:00+00:00","modifiedAt":"0001-01-01T00:00:00+00:00"}]"""
  )

  let result = tryReadFromFile path

  let expected =
    makeReading 1 120 80 70 (Timestamp.utc 2024 10 15 9 0 0) (Some "morning")

  test <@ result = Ok [ expected ] @>

[<Fact>]
let ``tryReadFromFile returns Ok with empty list when file contains empty array`` () =
  let path = Path.GetTempFileName()
  File.WriteAllText(path, "[]")
  let result = tryReadFromFile path
  test <@ result = Ok [] @>

[<Fact>]
let ``tryReadFromFile returns Error when file does not exist`` () =
  let result = tryReadFromFile "/nonexistent/path/file.json"
  test <@ result <> Ok [] @>

[<Fact>]
let ``tryReadFromFile returns Error when file contains invalid JSON`` () =
  let path = Path.GetTempFileName()
  File.WriteAllText(path, "not valid json")
  let result = tryReadFromFile path
  test <@ result <> Ok [] @>

[<Fact>]
let ``ensureFileExists - creates file with empty array when file does not exist`` () =
  let path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())

  try
    ensureFileExists path

    test <@ File.Exists(path) @>
    test <@ File.ReadAllText(path) = "[]" @>
  finally
    File.Delete(path)

[<Fact>]
let ``ensureFileExists - does not overwrite existing file`` () =
  let path = Path.GetTempFileName()

  try
    File.WriteAllText(path, "existing content")
    ensureFileExists path

    test <@ File.ReadAllText(path) = "existing content" @>
  finally
    File.Delete(path)

[<Fact>]
let ``import - new reading is added to repository`` () =
  let repo = InMemoryReadingRepository(Some []) :> IReadingRepository

  let reading = makeReading 99 120 80 70 (Timestamp.utc 2024 10 15 9 0 0) None

  let result = import repo [ reading ]
  test <@ result.Added = 1 @>
  test <@ result.Updated = 0 @>

[<Fact>]
let ``import - existing reading with same timestamp is updated`` () =
  let ts = Timestamp.utc 2024 10 15 9 0 0
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
