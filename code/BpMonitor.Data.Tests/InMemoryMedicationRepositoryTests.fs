module InMemoryMedicationRepositoryTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Data

let private defaultMemberId = 1

let private sample: Medication =
  { Id = 0
    MemberId = 0
    Name = "HCTZ"
    FullName = Some "hydrochlorothiazide"
    Comment = None
    StartDate = DateOnly(2026, 1, 1)
    EndDate = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

[<Fact>]
let ``GetAll returns empty list on startup`` () =
  let repo = InMemoryMedicationRepository(None) :> IMedicationRepository
  test <@ repo.GetAll(defaultMemberId) = [] @>

[<Fact>]
let ``Add makes medication available via GetAll`` () =
  let repo = InMemoryMedicationRepository(Some []) :> IMedicationRepository
  repo.Add defaultMemberId sample
  test <@ repo.GetAll(defaultMemberId).Length = 1 @>

[<Fact>]
let ``Add stamps the medication with the given memberId`` () =
  let repo = InMemoryMedicationRepository(Some []) :> IMedicationRepository
  repo.Add 2 sample
  test <@ repo.GetAll(2).Length = 1 @>
  test <@ repo.GetAll(2).[0].MemberId = 2 @>
  test <@ repo.GetAll(defaultMemberId).Length = 0 @>

[<Fact>]
let ``Delete removes the medication`` () =
  let repo = InMemoryMedicationRepository(Some []) :> IMedicationRepository
  repo.Add defaultMemberId sample
  let added = repo.GetAll(defaultMemberId)[0]
  repo.Delete defaultMemberId added.Id
  test <@ repo.GetAll(defaultMemberId) = [] @>
