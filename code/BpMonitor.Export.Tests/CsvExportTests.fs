module CsvExportTests

open System.IO
open System.Threading.Tasks
open BpMonitor.Core
open BpMonitor.Export.CsvExport

open Swensen.Unquote
open VerifyXunit
open Xunit

let private reading =
  { Id = 1
    MemberId = 1
    Systolic = 120
    Diastolic = 80
    HeartRate = 70
    Timestamp = Timestamp.utc 2024 10 15 9 0 0
    Comments = Some "morning"
    CreatedAt = Timestamp.utc 2024 10 15 9 0 0
    ModifiedAt = Timestamp.utc 2024 10 15 9 0 0 }

[<Fact>]
let ``serialize readings to CSV matches snapshot`` () : Task =
  let csv = serialize [ reading ]
  Verifier.Verify(csv).ToTask()

[<Fact>]
let ``serialize produces a header row`` () =
  let csv = serialize []
  test <@ csv.StartsWith "Id,MemberId,Systolic,Diastolic,HeartRate,Timestamp,Comments,CreatedAt,ModifiedAt" @>

[<Fact>]
let ``serialize produces one data row per reading`` () =
  let csv = serialize [ reading; reading ]
  // 1 header + 2 data rows + trailing newline = 4 lines when split
  let lines = csv.Split('\n') |> Array.filter (fun l -> l.Trim() <> "")
  test <@ lines.Length = 3 @>

[<Fact>]
let ``serialize quotes a comment containing a comma`` () =
  let r = { reading with Comments = Some "a,b" }
  let csv = serialize [ r ]
  test <@ csv.Contains "\"a,b\"" @>

[<Fact>]
let ``serialize doubles embedded double-quotes in comments`` () =
  let r = { reading with Comments = Some "a\"b" }
  let csv = serialize [ r ]
  test <@ csv.Contains "\"a\"\"b\"" @>

[<Fact>]
let ``serialize emits an empty field for None comments`` () =
  let r = { reading with Comments = None }
  let csv = serialize [ r ]
  // The Comments column is between HeartRate and CreatedAt; an empty field
  // produces two adjacent commas.
  test <@ csv.Contains ",," @>

[<Fact>]
let ``tryWriteToFile writes serialized readings to the given path`` () =
  let path = Path.GetTempFileName()
  tryWriteToFile path [ reading ] |> ignore

  let csv = File.ReadAllText(path)
  test <@ csv.Contains "120" && csv.Contains "morning" @>

[<Fact>]
let ``tryWriteToFile returns Ok when write succeeds`` () =
  let path = Path.GetTempFileName()
  let result = tryWriteToFile path []

  test <@ result = Ok() @>

[<Fact>]
let ``tryWriteToFile returns Error when path is invalid`` () =
  let result = tryWriteToFile "/invalid/path/that/does/not/exist/file.csv" []

  test <@ result <> Ok() @>
