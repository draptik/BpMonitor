module JsonExportTests

open System.IO
open System.Text.Json
open System.Threading.Tasks
open BpMonitor.Core
open BpMonitor.Export.JsonExport
open BpMonitor.TestSupport

open Swensen.Unquote
open Xunit

let private thisFile = Path.Combine(__SOURCE_DIRECTORY__, __SOURCE_FILE__)
let private verifyJson = Verifier.verifyJson thisFile

[<Fact>]
let ``serialize readings to JSON matches snapshot`` () : Task =
  let reading =
    { Id = 1
      MemberId = 1
      Systolic = 120
      Diastolic = 80
      HeartRate = 70
      Timestamp = Timestamp.utc 2024 10 15 9 0 0
      Comments = Some "morning"
      CreatedAt = Timestamp.utc 2024 10 15 9 0 0
      ModifiedAt = Timestamp.utc 2024 10 15 9 0 0 }

  let json = serialize [ reading ]
  verifyJson json

[<Fact>]
let ``serialize readings with no comments matches snapshot`` () : Task =
  let reading =
    { Id = 1
      MemberId = 1
      Systolic = 120
      Diastolic = 80
      HeartRate = 70
      Timestamp = Timestamp.utc 2024 10 15 9 0 0
      Comments = None
      CreatedAt = Timestamp.utc 2024 10 15 9 0 0
      ModifiedAt = Timestamp.utc 2024 10 15 9 0 0 }

  let json = serialize [ reading ]
  verifyJson json

[<Fact>]
let ``serialize emits an array entry per reading in input order`` () =
  let first =
    { Id = 1
      MemberId = 1
      Systolic = 120
      Diastolic = 80
      HeartRate = 70
      Timestamp = Timestamp.utc 2024 10 15 9 0 0
      Comments = None
      CreatedAt = Timestamp.utc 2024 10 15 9 0 0
      ModifiedAt = Timestamp.utc 2024 10 15 9 0 0 }

  let second = { first with Id = 2; Systolic = 140 }

  let json = serialize [ first; second ]
  let root = JsonDocument.Parse(json).RootElement

  let length = root.GetArrayLength()
  let firstId = root.[0].GetProperty("id").GetInt32()
  let firstSystolic = root.[0].GetProperty("systolic").GetInt32()
  let secondId = root.[1].GetProperty("id").GetInt32()
  let secondSystolic = root.[1].GetProperty("systolic").GetInt32()

  test <@ length = 2 @>
  test <@ firstId = 1 @>
  test <@ firstSystolic = 120 @>
  test <@ secondId = 2 @>
  test <@ secondSystolic = 140 @>

[<Fact>]
let ``tryWriteToFile writes serialized readings to the given path`` () =
  let reading =
    { Id = 1
      MemberId = 1
      Systolic = 120
      Diastolic = 80
      HeartRate = 70
      Timestamp = Timestamp.utc 2024 10 15 9 0 0
      Comments = None
      CreatedAt = Timestamp.utc 2024 10 15 9 0 0
      ModifiedAt = Timestamp.utc 2024 10 15 9 0 0 }

  let path = Path.GetTempFileName()
  tryWriteToFile path [ reading ] |> ignore

  let json = File.ReadAllText(path)
  let root = JsonDocument.Parse(json).RootElement
  let length = root.GetArrayLength()

  test <@ length = 1 @>

[<Fact>]
let ``tryWriteToFile returns Ok when write succeeds`` () =
  let path = Path.GetTempFileName()
  let result = tryWriteToFile path []

  test <@ result = Ok() @>

[<Fact>]
let ``tryWriteToFile returns Error when path is invalid`` () =
  let result = tryWriteToFile "/invalid/path/that/does/not/exist/file.json" []

  test <@ result <> Ok() @>
