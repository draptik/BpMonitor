module BpMonitor.Import.JsonImport

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open BpMonitor.Core

let private options =
  let o = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
  o.Converters.Add(JsonFSharpConverter())
  o

type JsonImportSummary = { Added: int; Updated: int }

let parse (json: string) : Result<BloodPressureReading list, string> =
  try
    Ok(JsonSerializer.Deserialize<BloodPressureReading list>(json, options))
  with :? JsonException as ex ->
    Error ex.Message

let tryReadFromFile (path: string) : Result<BloodPressureReading list, string> =
  try
    let json = File.ReadAllText(path)
    Ok(JsonSerializer.Deserialize<BloodPressureReading list>(json, options))
  with
  | :? IOException as ex -> Error ex.Message
  | :? UnauthorizedAccessException as ex -> Error ex.Message
  | :? JsonException as ex -> Error ex.Message

let import (repository: IReadingRepository) (readings: BloodPressureReading list) : JsonImportSummary =
  let existing = repository.GetAll()

  let folder (added, updated) (reading: BloodPressureReading) =
    match existing |> List.tryFind (fun r -> r.Timestamp = reading.Timestamp) with
    | None ->
      repository.Add(reading)
      (added + 1, updated)
    | Some existingReading ->
      repository.Update({ reading with Id = existingReading.Id })
      (added, updated + 1)

  let (added, updated) = readings |> List.fold folder (0, 0)
  { Added = added; Updated = updated }
