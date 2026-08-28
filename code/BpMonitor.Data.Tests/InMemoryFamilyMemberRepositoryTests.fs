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
        Id = 999 }

  repo.Update(ghost)
  test <@ repo.GetAll() = [] @>
