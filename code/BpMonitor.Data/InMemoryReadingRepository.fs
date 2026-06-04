namespace BpMonitor.Data

open System
open BpMonitor.Core

module private Defaults =
  let readings =
    [ { Id = 1
        MemberId = 1
        Systolic = 122
        Diastolic = 78
        HeartRate = 65
        Timestamp = Timestamp.utc 2026 2 28 7 30 0
        Comments = None
        CreatedAt = DateTimeOffset.MinValue
        ModifiedAt = DateTimeOffset.MinValue }
      { Id = 2
        MemberId = 1
        Systolic = 135
        Diastolic = 88
        HeartRate = 78
        Timestamp = Timestamp.utc 2026 3 1 8 0 0
        Comments = Some "After coffee"
        CreatedAt = DateTimeOffset.MinValue
        ModifiedAt = DateTimeOffset.MinValue }
      { Id = 3
        MemberId = 1
        Systolic = 118
        Diastolic = 74
        HeartRate = 62
        Timestamp = Timestamp.utc 2026 3 2 7 45 0
        Comments = Some "Morning, rested"
        CreatedAt = DateTimeOffset.MinValue
        ModifiedAt = DateTimeOffset.MinValue }
      { Id = 4
        MemberId = 1
        Systolic = 128
        Diastolic = 82
        HeartRate = 70
        Timestamp = Timestamp.utc 2026 3 3 9 15 0
        Comments = None
        CreatedAt = DateTimeOffset.MinValue
        ModifiedAt = DateTimeOffset.MinValue } ]

type InMemoryReadingRepository(initialReadings: BloodPressureReading list option) =
  let readings =
    ResizeArray<BloodPressureReading>(defaultArg initialReadings Defaults.readings)

  let mutable nextId =
    let initial = defaultArg initialReadings Defaults.readings

    if initial.IsEmpty then
      1
    else
      (initial |> List.map _.Id |> List.max) + 1

  interface IReadingRepository with
    member _.GetAll(memberId) =
      readings |> Seq.filter (fun r -> r.MemberId = memberId) |> Seq.toList

    member _.Add memberId reading =
      readings.Add(
        { reading with
            Id = nextId
            MemberId = memberId }
      )

      nextId <- nextId + 1

    member _.AddMany memberId newReadings =
      newReadings
      |> List.iter (fun r ->
        readings.Add(
          { r with
              Id = nextId
              MemberId = memberId }
        )

        nextId <- nextId + 1)

    member _.Update(reading) =
      let idx =
        readings
        |> Seq.findIndex (fun r -> r.Id = reading.Id && r.MemberId = reading.MemberId)

      readings[idx] <- reading
