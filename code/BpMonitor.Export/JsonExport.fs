module BpMonitor.Export.JsonExport

open System.Text.Json
open System.Text.Json.Serialization
open BpMonitor.Core

let private options =
  let o = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
  o.Converters.Add(JsonFSharpConverter())
  o

let serialize (readings: BloodPressureReading list) : string =
  JsonSerializer.Serialize(readings, options)
