module CsvExportPropertyTests

open System
open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Core
open BpMonitor.Export.CsvExport
open BpMonitor.TestSupport

let private ts = Timestamp.utc 2024 10 15 9 0 0

let private reading comment =
  {
    Id = 1
    MemberId = 1
    Systolic = 120
    Diastolic = 80
    HeartRate = 70
    Timestamp = ts
    Comments = Some comment
    CreatedAt = ts
    ModifiedAt = ts
  }

// Bias generated comments toward leading characters that matter to CSV and spreadsheets.
let private commentGen =
  gen {
    let! lead = Gen.elements [ ""; "="; "+"; "-"; "@"; "\t"; "\r"; "\n"; "'"; ","; "\"" ]

    let! rest =
      ArbMap.defaults
      |> ArbMap.generate<string>
      |> Gen.map (Option.ofObj >> Option.defaultValue "")

    return lead + rest
  }

let private formulaTriggers = [ '='; '+'; '-'; '@'; '\t'; '\r' ]

// Decodes the Comments field of a single-row export; None if it isn't a valid RFC 4180 field.
let private decodeComment (csv: string) : string option =
  let nl = Environment.NewLine
  let o = ts.ToString("o")
  let prefix = $"{csv.Split(nl)[0]}{nl}1,1,120,80,70,{o},"
  let suffix = $",{o},{o}{nl}"

  if csv.StartsWith prefix && csv.EndsWith suffix then
    let field = csv.Substring(prefix.Length, csv.Length - prefix.Length - suffix.Length)

    if field.Length >= 2 && field.StartsWith "\"" && field.EndsWith "\"" then
      let inner = field.Substring(1, field.Length - 2)

      if inner.Replace("\"\"", "").Contains "\"" then
        None
      else
        Some(inner.Replace("\"\"", "\""))
    elif field.IndexOfAny [| ','; '"'; '\n'; '\r' |] >= 0 then
      None
    else
      Some field
  else
    None

[<Property>]
let ``serialized comment never starts with a formula trigger`` () =
  Prop.forAll (Arb.fromGen commentGen) (fun comment ->
    match decodeComment (serialize [ reading comment ]) with
    | Some field -> field.Length = 0 || not (List.contains field[0] formulaTriggers)
    | None -> false)

[<Property>]
let ``serialized comment decodes to the original, with at most a leading apostrophe added`` () =
  Prop.forAll (Arb.fromGen commentGen) (fun comment ->
    match decodeComment (serialize [ reading comment ]) with
    | Some field -> field = comment || field = "'" + comment
    | None -> false)
