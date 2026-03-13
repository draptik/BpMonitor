module BpMonitor.Export.Tests.JsonExportTests

open System
open System.IO
open System.Text.Json
open System.Threading.Tasks
open BpMonitor.Core
open BpMonitor.Export.JsonExport
open Swensen.Unquote
open VerifyXunit
open Xunit

[<Fact>]
let ``serialize readings to JSON matches snapshot`` () : Task =
  let reading =
    { Id = 1
      Systolic = 120
      Diastolic = 80
      HeartRate = 70
      Timestamp = DateTimeOffset(2024, 10, 15, 9, 0, 0, TimeSpan.Zero)
      Comments = Some "morning"
      CreatedAt = DateTimeOffset(2024, 10, 15, 9, 0, 0, TimeSpan.Zero)
      ModifiedAt = DateTimeOffset(2024, 10, 15, 9, 0, 0, TimeSpan.Zero) }

  let json = serialize [ reading ]
  Verifier.VerifyJson(json).ToTask()

[<Fact>]
let ``tryWriteToFile writes serialized readings to the given path`` () =
  let reading =
    { Id = 1
      Systolic = 120
      Diastolic = 80
      HeartRate = 70
      Timestamp = DateTimeOffset(2024, 10, 15, 9, 0, 0, TimeSpan.Zero)
      Comments = None
      CreatedAt = DateTimeOffset(2024, 10, 15, 9, 0, 0, TimeSpan.Zero)
      ModifiedAt = DateTimeOffset(2024, 10, 15, 9, 0, 0, TimeSpan.Zero) }

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
