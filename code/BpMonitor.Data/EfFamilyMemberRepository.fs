namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore
open BpMonitor.Core

module private MemberMapping =
  let toDomain (r: MemberRecord) : FamilyMember =
    { Id = r.Id
      Name = r.Name
      IsAdmin = r.IsAdmin
      IsActive = r.IsActive
      PasswordHash =
        if System.String.IsNullOrEmpty(r.PasswordHash) then
          None
        else
          Some r.PasswordHash
      CreatedAt = r.CreatedAt
      ModifiedAt = r.ModifiedAt }

  let toEntity (now: System.DateTimeOffset) (m: FamilyMember) : MemberRecord =
    { Id = m.Id
      Name = m.Name
      IsAdmin = m.IsAdmin
      IsActive = m.IsActive
      PasswordHash = m.PasswordHash |> Option.defaultValue ""
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

    member _.Update(m) =
      // Guard: only update if the member exists (prevents DbUpdateConcurrencyException on missing rows).
      let exists = ctx.Members.AsNoTracking() |> Seq.exists (fun r -> r.Id = m.Id)

      if exists then
        let now = timeProvider.GetUtcNow()

        // Detach any tracked entity with the same Id to avoid EF tracking conflicts.
        ctx.ChangeTracker.Entries<MemberRecord>()
        |> Seq.tryFind (fun e -> e.Entity.Id = m.Id)
        |> Option.iter (fun e -> e.State <- Microsoft.EntityFrameworkCore.EntityState.Detached)

        let entity: MemberRecord =
          { Id = m.Id
            Name = m.Name
            IsAdmin = m.IsAdmin
            IsActive = m.IsActive
            PasswordHash = m.PasswordHash |> Option.defaultValue ""
            CreatedAt = m.CreatedAt
            ModifiedAt = now }

        ctx.Members.Update(entity) |> ignore
        ctx.SaveChanges() |> ignore
