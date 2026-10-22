module MedicationRepositoryContractTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.TestSupport.TestBuilders

let private medication name =
  mkMedication 0 0 name (DateOnly(2026, 1, 1))

/// Adds `m` for `memberId` and returns it as stored, with its assigned Id.
let private seed (repo: IMedicationRepository) memberId m =
  repo.Add memberId m
  repo.GetAll(memberId) |> List.maxBy _.Id

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``GetAll returns only medications for the requested member`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  let m1 = seed repo 1 (medication "HCTZ")
  let m2 = seed repo 2 (medication "lisinopril")
  test <@ repo.GetAll(1) |> List.length = 1 @>
  test <@ repo.GetAll(1).[0].Id = m1.Id @>
  test <@ repo.GetAll(2).[0].Id = m2.Id @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Add stamps the medication with the given memberId`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  repo.Add 2 (medication "HCTZ")
  test <@ repo.GetAll(2) |> List.length = 1 @>
  test <@ repo.GetAll(1) |> List.isEmpty @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update replaces the medication with the matching Id`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  let stored = seed repo 1 (medication "HCTZ")
  repo.Update({ stored with Name = "HCTZ 25mg" })
  test <@ repo.GetAll(1) |> List.exists (fun m -> m.Name = "HCTZ 25mg") @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update does not affect a medication belonging to a different member`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  let stored = seed repo 1 (medication "HCTZ")

  repo.Update(
    { stored with
        MemberId = 2
        Name = "renamed"
    }
  )

  test <@ repo.GetAll(1) |> List.exists (fun x -> x.Name = "HCTZ") @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Update of a non-existent medication is a no-op`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  let stored = seed repo 1 (medication "HCTZ")

  repo.Update(
    { stored with
        Id = 999
        Name = "renamed"
    }
  )

  test <@ repo.GetAll(1) = [ stored ] @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Delete removes the medication with the matching Id`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  let stored = seed repo 1 (medication "HCTZ")
  repo.Delete 1 stored.Id
  test <@ repo.GetAll(1) |> List.isEmpty @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Delete does not affect a medication belonging to a different member`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  let stored = seed repo 1 (medication "HCTZ")
  repo.Delete 2 stored.Id
  test <@ repo.GetAll(1) |> List.length = 1 @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``Delete of a non-existent medication is a no-op`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  let stored = seed repo 1 (medication "HCTZ")
  repo.Delete 1 999
  test <@ repo.GetAll(1) = [ stored ] @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``TryGet returns the member's medication with that Id`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  seed repo 1 (medication "HCTZ") |> ignore
  let stored = seed repo 1 (medication "lisinopril")
  test <@ repo.TryGet 1 stored.Id = Some stored @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``TryGet returns None for another member's medication`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  let stored = seed repo 2 (medication "HCTZ")
  test <@ repo.TryGet 1 stored.Id = None @>

[<Theory>]
[<InlineData("InMemory")>]
[<InlineData("Ef")>]
let ``TryGet returns None for an unknown medication Id`` (impl: string) =
  let repo, scope = RepositoryImplementations.medicationRepo impl
  use _ = scope
  seed repo 1 (medication "HCTZ") |> ignore
  test <@ repo.TryGet 1 999 = None @>
