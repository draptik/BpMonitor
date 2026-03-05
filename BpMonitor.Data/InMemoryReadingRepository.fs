namespace BpMonitor.Data

open System
open BpMonitor.Core

module private Defaults =
    let readings = [
        { Id = 1; Systolic = 122; Diastolic = 78; HeartRate = 65; Timestamp = DateTimeOffset(2026, 2, 28, 7, 30, 0, TimeSpan.Zero); Comments = None }
        { Id = 2; Systolic = 135; Diastolic = 88; HeartRate = 78; Timestamp = DateTimeOffset(2026, 3,  1, 8,  0, 0, TimeSpan.Zero); Comments = Some "After coffee" }
        { Id = 3; Systolic = 118; Diastolic = 74; HeartRate = 62; Timestamp = DateTimeOffset(2026, 3,  2, 7, 45, 0, TimeSpan.Zero); Comments = Some "Morning, rested" }
        { Id = 4; Systolic = 128; Diastolic = 82; HeartRate = 70; Timestamp = DateTimeOffset(2026, 3,  3, 9, 15, 0, TimeSpan.Zero); Comments = None }
    ]

type InMemoryReadingRepository(initialReadings: BloodPressureReading list option) =
    let readings = ResizeArray<BloodPressureReading>(defaultArg initialReadings Defaults.readings)

    interface IReadingRepository with
        member _.GetAll() =
            readings |> Seq.toList

        member _.Add(reading) =
            let nextId =
                if readings.Count = 0 then 1
                else (readings |> Seq.map (fun r -> r.Id) |> Seq.max) + 1
            readings.Add({ reading with Id = nextId })
