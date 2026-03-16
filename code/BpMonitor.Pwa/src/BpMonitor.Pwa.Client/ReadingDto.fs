module BpMonitor.Pwa.Client.ReadingDto

open System
open BpMonitor.Pwa.Client.Reading

[<CLIMutable>]
type ReadingDto =
  { Systolic: int
    Diastolic: int
    HeartRate: int
    Timestamp: string
    Comment: string }

let toDto (r: Reading) : ReadingDto =
  { Systolic = r.Systolic
    Diastolic = r.Diastolic
    HeartRate = r.HeartRate
    Timestamp = r.Timestamp.ToString("yyyy-MM-ddTHH:mm:sszzz")
    Comment = r.Comment |> Option.defaultValue null }

let fromDto (dto: ReadingDto) : Reading =
  { Systolic = dto.Systolic
    Diastolic = dto.Diastolic
    HeartRate = dto.HeartRate
    Timestamp = DateTimeOffset.Parse(dto.Timestamp)
    Comment = if isNull dto.Comment then None else Some dto.Comment }
