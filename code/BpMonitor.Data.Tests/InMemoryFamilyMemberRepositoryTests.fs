module InMemoryFamilyMemberRepositoryTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.TestSupport.TestBuilders
open BpMonitor.Data

[<Fact>]
let ``GetAll returns the default Me member on startup`` () =
  let repo = InMemoryFamilyMemberRepository(None) :> IFamilyMemberRepository
  test <@ repo.GetAll() |> List.map _.Name = [ "Me" ] @>

[<Fact>]
let ``Add assigns sequential Ids starting at 1 on an empty store`` () =
  let repo = InMemoryFamilyMemberRepository(Some []) :> IFamilyMemberRepository
  let first = repo.Add(mkMember 0 "Alice" true true)
  let second = repo.Add(mkMember 0 "Bob" false true)
  test <@ first.Id = 1 @>
  test <@ second.Id = 2 @>

[<Fact>]
let ``Add continues Id numbering above the highest existing Id`` () =
  let existing =
    { mkMember 0 "Alice" true true with
        Id = 5
    }

  let repo =
    InMemoryFamilyMemberRepository(Some [ existing ]) :> IFamilyMemberRepository

  let added = repo.Add(mkMember 0 "Bob" false true)
  test <@ added.Id = 6 @>
