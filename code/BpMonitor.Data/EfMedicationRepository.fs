namespace BpMonitor.Data

open System
open Microsoft.EntityFrameworkCore
open BpMonitor.Core

module private MedicationMapping =
  let toDomain (r: MedicationRecord) : Medication =
    {
      Id = r.Id
      MemberId = r.MemberId
      Name = r.Name
      FullName = Option.ofObj r.FullName
      Comment = Option.ofObj r.Comment
      StartDate = r.StartDate
      EndDate = Option.ofNullable r.EndDate
      CreatedAt = r.CreatedAt
      ModifiedAt = r.ModifiedAt
    }

  let withTimestamps (now: DateTimeOffset) (m: Medication) =
    { m with
        CreatedAt = now
        ModifiedAt = now
    }

  let withModifiedAt (now: DateTimeOffset) (m: Medication) = { m with ModifiedAt = now }

  let toEntity (m: Medication) : MedicationRecord =
    {
      Id = m.Id
      MemberId = m.MemberId
      Name = m.Name
      FullName = Option.toObj m.FullName
      Comment = Option.toObj m.Comment
      StartDate = m.StartDate
      EndDate = Option.toNullable m.EndDate
      CreatedAt = m.CreatedAt
      ModifiedAt = m.ModifiedAt
    }

type EfMedicationRepository(ctx: BpMonitorDbContext, timeProvider: TimeProvider) =
  interface IMedicationRepository with
    member _.GetAll(memberId) =
      query {
        for m in ctx.Medications.AsNoTracking() do
          where (m.MemberId = memberId)
          select m
      }
      |> Seq.map MedicationMapping.toDomain
      |> Seq.toList

    member _.TryGet memberId id =
      query {
        for m in ctx.Medications.AsNoTracking() do
          where (m.MemberId = memberId && m.Id = id)
          select m
      }
      |> Seq.tryHead
      |> Option.map MedicationMapping.toDomain

    member _.Add memberId medication =
      let now = timeProvider.GetUtcNow()

      ctx.Medications.Add(
        medication
        |> MedicationMapping.withTimestamps now
        |> (fun m -> { m with MemberId = memberId })
        |> MedicationMapping.toEntity
      )
      |> ignore

      ctx.SaveChanges() |> ignore

    // Matching MemberId too prevents cross-member writes.
    member _.Update(medication) =
      EfUpdate.ReplaceIfExists(
        ctx,
        ctx.Medications,
        (fun m -> m.Id = medication.Id && m.MemberId = medication.MemberId),
        _.Id,
        medication
        |> MedicationMapping.withModifiedAt (timeProvider.GetUtcNow())
        |> MedicationMapping.toEntity
      )

    member _.Delete memberId id =
      let entity =
        query {
          for m in ctx.Medications do
            where (m.Id = id && m.MemberId = memberId)
            select m
        }
        |> Seq.tryHead

      match entity with
      | Some e ->
        ctx.Medications.Remove(e) |> ignore
        ctx.SaveChanges() |> ignore
      | None -> ()
