module BpMonitor.Export.Tests.JsonExportTests

open System
open System.Text.Json
open BpMonitor.Core
open BpMonitor.Export.JsonExport
open Swensen.Unquote
open Xunit

[<Fact>]
let ``serialize readings to JSON produces expected output`` () =
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
  let root = JsonDocument.Parse(json).RootElement
  let length = root.GetArrayLength()
  let systolic = root.[0].GetProperty("systolic").GetInt32()
  let comments = root.[0].GetProperty("comments").GetString()

  test <@ length = 1 @>
  test <@ systolic = 120 @>
  test <@ comments = "morning" @>
