namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore
open BpMonitor.Core

module private Mapping =
  let toDomain (r: ReadingRecord) : BloodPressureReading =
    { Id = r.Id
      MemberId = r.MemberId
      Systolic = r.Systolic
      Diastolic = r.Diastolic
      HeartRate = r.HeartRate
      Timestamp = r.Timestamp
      Comments = if isNull r.Comments then None else Some r.Comments
      CreatedAt = r.CreatedAt
      ModifiedAt = r.ModifiedAt }

  let withTimestamps (now: System.DateTimeOffset) (r: BloodPressureReading) =
    { r with
        CreatedAt = now
        ModifiedAt = now }

  let withModifiedAt (now: System.DateTimeOffset) (r: BloodPressureReading) = { r with ModifiedAt = now }

  let toEntity (r: BloodPressureReading) : ReadingRecord =
    { Id = r.Id
      MemberId = r.MemberId
      Systolic = r.Systolic
      Diastolic = r.Diastolic
      HeartRate = r.HeartRate
      Timestamp = r.Timestamp
      Comments = r.Comments |> Option.defaultValue null
      CreatedAt = r.CreatedAt
      ModifiedAt = r.ModifiedAt }

type EfReadingRepository(ctx: BpMonitorDbContext, timeProvider: System.TimeProvider) =
  interface IReadingRepository with
    member _.GetAll(memberId) =
      ctx.Readings.AsNoTracking()
      |> Seq.filter (fun r -> r.MemberId = memberId)
      |> Seq.map Mapping.toDomain
      |> Seq.toList

    member _.Add memberId reading =
      let now = timeProvider.GetUtcNow()

      ctx.Readings.Add(
        reading
        |> Mapping.withTimestamps now
        |> (fun r -> { r with MemberId = memberId })
        |> Mapping.toEntity
      )
      |> ignore

      ctx.SaveChanges() |> ignore

    member _.AddMany memberId readings =
      let now = timeProvider.GetUtcNow()

      readings
      |> List.iter (fun r ->
        ctx.Readings.Add(
          r
          |> Mapping.withTimestamps now
          |> (fun r -> { r with MemberId = memberId })
          |> Mapping.toEntity
        )
        |> ignore)

      ctx.SaveChanges() |> ignore

    member _.Update(reading) =
      let now = timeProvider.GetUtcNow()

      // Guard: only update if reading belongs to the expected member (prevents cross-member writes)
      let existsForMember =
        ctx.Readings.AsNoTracking()
        |> Seq.exists (fun r -> r.Id = reading.Id && r.MemberId = reading.MemberId)

      if existsForMember then
        ctx.ChangeTracker.Entries<ReadingRecord>()
        |> Seq.tryFind (fun e -> e.Entity.Id = reading.Id)
        |> Option.iter (fun e -> e.State <- EntityState.Detached)

        ctx.Readings.Update(reading |> Mapping.withModifiedAt now |> Mapping.toEntity)
        |> ignore

        ctx.SaveChanges() |> ignore
