module BpMonitor.Export.CsvExport

open System.Text
open BpMonitor.Core

// RFC 4180: quote a field if it contains a comma, double-quote, CR, or LF;
// double any embedded double-quotes.
let private csvField (s: string) : string =
  if s.IndexOfAny [| ','; '"'; '\n'; '\r' |] >= 0 then
    "\"" + s.Replace("\"", "\"\"") + "\""
  else
    s

let private header =
  "Id,MemberId,Systolic,Diastolic,HeartRate,Timestamp,Comments,CreatedAt,ModifiedAt"

let private row (r: BloodPressureReading) : string =
  // Column order mirrors the JSON export; timestamps as raw ISO ("o").
  [ string r.Id
    string r.MemberId
    string r.Systolic
    string r.Diastolic
    string r.HeartRate
    r.Timestamp.ToString("o")
    csvField (r.Comments |> Option.defaultValue "")
    r.CreatedAt.ToString("o")
    r.ModifiedAt.ToString("o") ]
  |> String.concat ","

let serialize (readings: BloodPressureReading list) : string =
  let sb = StringBuilder().AppendLine(header)
  readings |> List.iter (fun r -> sb.AppendLine(row r) |> ignore)
  sb.ToString()

let tryWriteToFile (path: string) (readings: BloodPressureReading list) : Result<unit, string> =
  FileHelpers.tryWriteString path (serialize readings)
