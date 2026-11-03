module InMemoryMedicationRepositoryTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Data

let private defaultMemberId = 1

[<Fact>]
let ``GetAll returns empty list on startup`` () =
  let repo = InMemoryMedicationRepository(None) :> IMedicationRepository
  test <@ repo.GetAll(defaultMemberId) = [] @>
