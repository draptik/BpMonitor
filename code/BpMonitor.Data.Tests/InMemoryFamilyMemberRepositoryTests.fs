module InMemoryFamilyMemberRepositoryTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Data

let private newMember name isAdmin =
  FamilyMember.create name isAdmin
  |> Result.defaultWith (fun _ -> failwith "invalid member")

[<Fact>]
let ``GetAll returns the default Me member on startup`` () =
  let repo = InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository
  test <@ repo.GetAll() |> List.map _.Name = [ "Me" ] @>

[<Fact>]
let ``GetAll returns empty list when constructed with an empty member list`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository
  test <@ repo.GetAll() = [] @>

[<Fact>]
let ``GetById returns Some when member exists`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository
  let added = repo.Add(newMember "Alice" true)
  test <@ repo.GetById(added.Id) = Some added @>

[<Fact>]
let ``GetById returns None when member does not exist`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository
  test <@ repo.GetById(999) = None @>

[<Fact>]
let ``Add assigns sequential Ids starting at 1 on an empty store`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository
  let first = repo.Add(newMember "Alice" true)
  let second = repo.Add(newMember "Bob" false)
  test <@ first.Id = 1 @>
  test <@ second.Id = 2 @>

[<Fact>]
let ``Add continues Id numbering above the highest existing Id`` () =
  let existing = { newMember "Alice" true with Id = 5 }

  let repo =
    InMemoryFamilyMemberRepository(Some [ existing ]) :> IFamilyMemberRepository

  let added = repo.Add(newMember "Bob" false)
  test <@ added.Id = 6 @>

[<Fact>]
let ``Update modifies the stored member`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository
  let added = repo.Add(newMember "Alice" true)
  repo.Update { added with Name = "Alicia" }
  test <@ (repo.GetById added.Id).Value.Name = "Alicia" @>

[<Fact>]
let ``Update of a non-existent member is a no-op`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository

  let ghost =
    { newMember "Ghost" false with
        Id = 999
    }

  repo.Update(ghost)
  test <@ repo.GetAll() = [] @>

[<Fact>]
let ``UpdateLanguage changes only the language`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository
  let added = repo.Add(newMember "Alice" true)
  // An admin's change that a full-record write from an older snapshot would undo.
  repo.Update { added with IsActive = false }

  repo.UpdateLanguage added.Id German

  let stored = (repo.GetById added.Id).Value
  test <@ stored.Language = German && not stored.IsActive @>

[<Fact>]
let ``UpdateGoal changes only the goal range`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository
  let added = repo.Add(newMember "Alice" true)
  repo.Update { added with IsActive = false }

  let goal =
    {
      SystolicMin = 100
      SystolicMax = 130
      DiastolicMin = 65
      DiastolicMax = 85
    }

  repo.UpdateGoal added.Id goal

  let stored = (repo.GetById added.Id).Value
  test <@ stored.Goal = goal && not stored.IsActive @>

[<Fact>]
let ``UpdateLanguage of a non-existent member leaves every member unchanged`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository
  repo.Add(newMember "Alice" true) |> ignore
  let before = repo.GetAll()

  repo.UpdateLanguage 999 German

  test <@ repo.GetAll() = before @>

[<Fact>]
let ``UpdateGoal of a non-existent member leaves every member unchanged`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository
  repo.Add(newMember "Alice" true) |> ignore
  let before = repo.GetAll()

  repo.UpdateGoal
    999
    (GoalRange.create 100 130 65 85
     |> Result.defaultWith (fun _ -> failwith "invalid goal"))

  test <@ repo.GetAll() = before @>
