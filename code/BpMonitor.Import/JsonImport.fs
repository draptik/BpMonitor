module BpMonitor.Import.JsonImport

open System.Text.Json
open BpMonitor.Core
open BpMonitor.Export

type JsonImportSummary = { Added: int; Updated: int }

let parse (json: string) : Result<BloodPressureReading list, string> =
  try
    Ok(JsonExport.deserialize json)
  with :? JsonException as ex ->
    Error ex.Message

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
