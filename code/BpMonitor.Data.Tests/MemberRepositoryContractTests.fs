module MemberRepositoryContractTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.TestSupport.TestBuilders

// Differs from seedGoal and GoalRange.defaults, so the update is visible.
let private customGoal = mkGoal 100 130 65 85

// Non-default in every field an update could clobber, so a full-record write shows.
let private seedGoal = mkGoal 95 135 62 88

/// Adds a member with non-default IsActive, Language and Goal; returns it as stored.
let private seedCustomized (repo: IFamilyMemberRepository) =
  let added = repo.Add(mkMember 0 "Alice" true true)

  repo.Update
    { added with
        IsActive = false
        Language = German
        Goal = seedGoal
    }

  (repo.GetById added.Id).Value

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``GetAll returns empty list for an empty store`` (impl: string) =
  let repo, scope = RepositoryImplementations.memberRepo impl
  use _ = scope
  test <@ repo.GetAll() = [] @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``GetAll returns all members`` (impl: string) =
  let repo, scope = RepositoryImplementations.memberRepo impl
  use _ = scope
  let alice = repo.Add(mkMember 0 "Alice" true true)
  let bob = repo.Add(mkMember 0 "Bob" false true)
  test <@ repo.GetAll() |> List.sortBy _.Id = [ alice; bob ] @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``GetById returns Some when member exists`` (impl: string) =
  let repo, scope = RepositoryImplementations.memberRepo impl
  use _ = scope
  let added = repo.Add(mkMember 0 "Alice" true true)
  test <@ repo.GetById(added.Id) = Some added @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``GetById returns None when member does not exist`` (impl: string) =
  let repo, scope = RepositoryImplementations.memberRepo impl
  use _ = scope
  test <@ repo.GetById(999) = None @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update modifies the stored member`` (impl: string) =
  let repo, scope = RepositoryImplementations.memberRepo impl
  use _ = scope
  let added = repo.Add(mkMember 0 "Alice" true true)
  repo.Update { added with Name = "Alicia" }
  test <@ (repo.GetById added.Id).Value.Name = "Alicia" @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update of a non-existent member is a no-op`` (impl: string) =
  let repo, scope = RepositoryImplementations.memberRepo impl
  use _ = scope

  repo.Update
    { mkMember 0 "Ghost" false true with
        Id = 999
    }

  test <@ repo.GetAll() = [] @>

// ModifiedAt is excluded: EF stamps it (EfFamilyMemberRepositoryTests); InMemory has no clock.
[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``UpdateLanguage changes only the language`` (impl: string) =
  let repo, scope = RepositoryImplementations.memberRepo impl
  use _ = scope
  let before = seedCustomized repo

  repo.UpdateLanguage before.Id English

  let stored = (repo.GetById before.Id).Value

  test
    <@
      { stored with
          ModifiedAt = before.ModifiedAt
      }
        =
        { before with Language = English }
    @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``UpdateGoal changes only the goal range`` (impl: string) =
  let repo, scope = RepositoryImplementations.memberRepo impl
  use _ = scope
  let before = seedCustomized repo

  repo.UpdateGoal before.Id customGoal

  let stored = (repo.GetById before.Id).Value

  test
    <@
      { stored with
          ModifiedAt = before.ModifiedAt
      }
        =
        { before with Goal = customGoal }
    @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``UpdateLanguage of a non-existent member leaves every member unchanged`` (impl: string) =
  let repo, scope = RepositoryImplementations.memberRepo impl
  use _ = scope
  repo.Add(mkMember 0 "Alice" true true) |> ignore
  let before = repo.GetAll()

  repo.UpdateLanguage 999 German

  test <@ repo.GetAll() = before @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``UpdateGoal of a non-existent member leaves every member unchanged`` (impl: string) =
  let repo, scope = RepositoryImplementations.memberRepo impl
  use _ = scope
  repo.Add(mkMember 0 "Alice" true true) |> ignore
  let before = repo.GetAll()

  repo.UpdateGoal 999 customGoal

  test <@ repo.GetAll() = before @>
