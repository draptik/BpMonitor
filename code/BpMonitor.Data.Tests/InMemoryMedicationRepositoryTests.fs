module InMemoryMedicationRepositoryTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Data

let private defaultMemberId = 1

let private sample: Medication =
  {
    Id = 0
    MemberId = 0
    Name = "HCTZ"
    FullName = Some "hydrochlorothiazide"
    Comment = None
    StartDate = DateOnly(2026, 1, 1)
    EndDate = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue
  }

[<Fact>]
let ``GetAll returns empty list on startup`` () =
  let repo = InMemoryMedicationRepository(None) :> IMedicationRepository
  test <@ repo.GetAll(defaultMemberId) = [] @>
