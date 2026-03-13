module BpMonitor.Export.JsonExport

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

let tryWriteToFile (path: string) (readings: BloodPressureReading list) : Result<unit, string> =
  try
    File.WriteAllText(path, serialize readings)
    Ok()
  with ex ->
    Error ex.Message
