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
      Comments = if isNull r.Comments then None else Some r.Comments
      CreatedAt = r.CreatedAt
      ModifiedAt = r.ModifiedAt }

  let toEntity (r: BloodPressureReading) : ReadingRecord =
    { Id = r.Id
      Systolic = r.Systolic
      Diastolic = r.Diastolic
      HeartRate = r.HeartRate
      Timestamp = r.Timestamp
      Comments = r.Comments |> Option.defaultValue null
      CreatedAt = r.CreatedAt
      ModifiedAt = r.ModifiedAt }

type EfReadingRepository(ctx: BpMonitorDbContext, timeProvider: System.TimeProvider) =
  interface IReadingRepository with
    member _.GetAll() =
      ctx.Readings.AsNoTracking() |> Seq.map Mapping.toDomain |> Seq.toList

    member _.Add(reading) =
      let now = timeProvider.GetUtcNow()

      ctx.Readings.Add(
        Mapping.toEntity
          { reading with
              CreatedAt = now
              ModifiedAt = now }
      )
      |> ignore

      ctx.SaveChanges() |> ignore

    member _.AddMany(readings) =
      let now = timeProvider.GetUtcNow()

      readings
      |> List.iter (fun r ->
        ctx.Readings.Add(
          Mapping.toEntity
            { r with
                CreatedAt = now
                ModifiedAt = now }
        )
        |> ignore)

      ctx.SaveChanges() |> ignore

    member _.Update(reading) =
      let now = timeProvider.GetUtcNow()

      ctx.ChangeTracker.Entries<ReadingRecord>()
      |> Seq.tryFind (fun e -> e.Entity.Id = reading.Id)
      |> Option.iter (fun e -> e.State <- Microsoft.EntityFrameworkCore.EntityState.Detached)

      ctx.Readings.Update(Mapping.toEntity { reading with ModifiedAt = now })
      |> ignore

      ctx.SaveChanges() |> ignore
