namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore
open BpMonitor.Core

module private MedicationMapping =
  let toDomain (r: MedicationRecord) : Medication =
    { Id = r.Id
      MemberId = r.MemberId
      Name = r.Name
      FullName = Option.ofObj r.FullName
      Comment = Option.ofObj r.Comment
      StartDate = r.StartDate
      EndDate = Option.ofNullable r.EndDate
      CreatedAt = r.CreatedAt
      ModifiedAt = r.ModifiedAt }

  let withTimestamps (now: System.DateTimeOffset) (m: Medication) =
    { m with
        CreatedAt = now
        ModifiedAt = now }

  let withModifiedAt (now: System.DateTimeOffset) (m: Medication) = { m with ModifiedAt = now }

  let toEntity (m: Medication) : MedicationRecord =
    { Id = m.Id
      MemberId = m.MemberId
      Name = m.Name
      FullName = Option.toObj m.FullName
      Comment = Option.toObj m.Comment
      StartDate = m.StartDate
      EndDate = Option.toNullable m.EndDate
      CreatedAt = m.CreatedAt
      ModifiedAt = m.ModifiedAt }

type EfMedicationRepository(ctx: BpMonitorDbContext, timeProvider: System.TimeProvider) =
  interface IMedicationRepository with
    member _.GetAll(memberId) =
      query {
        for m in ctx.Medications.AsNoTracking() do
          where (m.MemberId = memberId)
          select m
      }
      |> Seq.map MedicationMapping.toDomain
      |> Seq.toList

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

    member _.Update(medication) =
      let now = timeProvider.GetUtcNow()

      let existsForMember =
        query {
          for m in ctx.Medications.AsNoTracking() do
            where (m.Id = medication.Id && m.MemberId = medication.MemberId)
            select m
        }
        |> Seq.isEmpty
        |> not

      if existsForMember then
        ctx.ChangeTracker.Entries<MedicationRecord>()
        |> Seq.tryFind (fun e -> e.Entity.Id = medication.Id)
        |> Option.iter (fun e -> e.State <- EntityState.Detached)

        ctx.Medications.Update(medication |> MedicationMapping.withModifiedAt now |> MedicationMapping.toEntity)
        |> ignore

        ctx.SaveChanges() |> ignore

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
