namespace BpMonitor.Data

open System
open BpMonitor.Core

module private MemberDefaults =
  let members =
    [ { Id = 1
        Name = "Me"
        IsAdmin = true
        IsActive = true
        PasswordHash = None
        Goal = GoalRange.defaults
        CreatedAt = DateTimeOffset.MinValue
        ModifiedAt = DateTimeOffset.MinValue } ]

type InMemoryFamilyMemberRepository(initialMembers: FamilyMember list option) =
  let members =
    ResizeArray<FamilyMember>(defaultArg initialMembers MemberDefaults.members)

  let mutable nextId =
    let initial = defaultArg initialMembers MemberDefaults.members

    if initial.IsEmpty then
      1
    else
      (initial |> List.map _.Id |> List.max) + 1

  interface IFamilyMemberRepository with
    member _.GetAll() = members |> Seq.toList

    member _.GetById(id) =
      members |> Seq.tryFind (fun m -> m.Id = id)

    member _.Add(m) =
      let created = { m with Id = nextId }
      members.Add(created)
      nextId <- nextId + 1
      created

    member _.Update(m) =
      let idx = members |> Seq.tryFindIndex (fun r -> r.Id = m.Id)

      match idx with
      | Some i -> members[i] <- m
      | None -> ()
