namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore
open BpMonitor.Core

module private Mapping =
  let toDomain (r: ReadingRecord) : BloodPressureReading =
    { Id = r.Id
      Systolic = r.Systolic
      Diastolic = r.Diastolic
      HeartRate = r.HeartRate
      Timestamp = r.Timestamp
      Comments =
        if obj.ReferenceEquals(r.Comments, null) then
          None
        else
          Some r.Comments }

  let toEntity (r: BloodPressureReading) : ReadingRecord =
    { Id = r.Id
      Systolic = r.Systolic
      Diastolic = r.Diastolic
      HeartRate = r.HeartRate
      Timestamp = r.Timestamp
      Comments = r.Comments |> Option.defaultValue null }

type EfReadingRepository(ctx: BpMonitorDbContext) =
  interface IReadingRepository with
    member _.GetAll() =
      ctx.Readings.AsNoTracking() |> Seq.map Mapping.toDomain |> Seq.toList

    member _.Add(reading) =
      ctx.Readings.Add(Mapping.toEntity reading) |> ignore
      ctx.SaveChanges() |> ignore

    member _.Update(reading) =
      ctx.Readings.Update(Mapping.toEntity reading) |> ignore
      ctx.SaveChanges() |> ignore
