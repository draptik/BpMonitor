module BpMonitor.Export.JsonExport

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open BpMonitor.Core

let private options =
  let o = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
  o.Converters.Add(JsonFSharpConverter())
  o

let serialize (readings: BloodPressureReading list) : string =
  JsonSerializer.Serialize(readings, options)

let deserialize (json: string) : BloodPressureReading list =
  JsonSerializer.Deserialize<BloodPressureReading list>(json, options)

let tryWriteToFile (path: string) (readings: BloodPressureReading list) : Result<unit, string> =
  try
    File.WriteAllText(path, serialize readings)
    Ok()
  with
  | :? IOException as ex -> Error ex.Message
  | :? UnauthorizedAccessException as ex -> Error ex.Message

let tryReadFromFile (path: string) : Result<BloodPressureReading list, string> =
  try
    let json = File.ReadAllText(path)
    Ok(deserialize json)
  with
  | :? IOException as ex -> Error ex.Message
  | :? UnauthorizedAccessException as ex -> Error ex.Message
  | :? Text.Json.JsonException as ex -> Error ex.Message
