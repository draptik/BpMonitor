namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore
open BpMonitor.Core

module private MemberMapping =
  let toDomain (r: MemberRecord) : FamilyMember =
    { Id = r.Id
      Name = r.Name
      CreatedAt = r.CreatedAt
      ModifiedAt = r.ModifiedAt }

  let toEntity (now: System.DateTimeOffset) (m: FamilyMember) : MemberRecord =
    { Id = m.Id
      Name = m.Name
      CreatedAt = now
      ModifiedAt = now }

type EfFamilyMemberRepository(ctx: BpMonitorDbContext, timeProvider: System.TimeProvider) =
  interface IFamilyMemberRepository with
    member _.GetAll() =
      ctx.Members.AsNoTracking() |> Seq.map MemberMapping.toDomain |> Seq.toList

    member _.GetById(id) =
      ctx.Members.AsNoTracking()
      |> Seq.tryFind (fun m -> m.Id = id)
      |> Option.map MemberMapping.toDomain

    member _.Add(m) =
      let now = timeProvider.GetUtcNow()
      let entity = MemberMapping.toEntity now m
      let entry = ctx.Members.Add(entity)
      ctx.SaveChanges() |> ignore
      MemberMapping.toDomain entry.Entity
