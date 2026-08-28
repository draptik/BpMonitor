module MedicationRepositoryContractTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.TestSupport.TestBuilders

type private StubRepository(initial: Medication list) =
  let medications = ResizeArray<Medication>(initial)

  interface IMedicationRepository with
    member _.GetAll(memberId) =
      medications |> Seq.filter (fun m -> m.MemberId = memberId) |> Seq.toList

    member _.Add memberId m =
      medications.Add({ m with MemberId = memberId })

    member _.Update(m) =
      let idx =
        medications
        |> Seq.tryFindIndex (fun x -> x.Id = m.Id && x.MemberId = m.MemberId)

      match idx with
      | Some i -> medications[i] <- m
      | None -> ()

    member _.Delete memberId id =
      let idx =
        medications |> Seq.tryFindIndex (fun x -> x.Id = id && x.MemberId = memberId)

      match idx with
      | Some i -> medications.RemoveAt(i)
      | None -> ()

let private medication id memberId name =
  mkMedication id memberId name (System.DateOnly(2026, 1, 1))

[<Fact>]
let ``GetAll returns only medications for the requested member`` () =
  let m1 = medication 1 1 "HCTZ"
  let m2 = medication 2 2 "lisinopril"
  let repo = StubRepository([ m1; m2 ]) :> IMedicationRepository
  test <@ repo.GetAll(1) |> List.length = 1 @>
  test <@ repo.GetAll(1).[0].Id = 1 @>
  test <@ repo.GetAll(2).[0].Id = 2 @>

[<Fact>]
let ``Add stamps the medication with the given memberId`` () =
  let repo = StubRepository([]) :> IMedicationRepository
  repo.Add 2 (medication 0 0 "HCTZ")
  test <@ repo.GetAll(2) |> List.length = 1 @>
  test <@ repo.GetAll(1) |> List.isEmpty @>

[<Fact>]
let ``Update replaces the medication with the matching Id`` () =
  let repo = StubRepository([ medication 1 1 "HCTZ" ]) :> IMedicationRepository

  let updated =
    { medication 1 1 "HCTZ 25mg" with
        Id = 1 }

  repo.Update(updated)
  test <@ repo.GetAll(1) |> List.exists (fun m -> m.Name = "HCTZ 25mg") @>

[<Fact>]
let ``Update does not affect a medication belonging to a different member`` () =
  let m = medication 1 1 "HCTZ"
  let repo = StubRepository([ m ]) :> IMedicationRepository
  repo.Update({ medication 1 2 "renamed" with Id = 1 })
  test <@ repo.GetAll(1) |> List.exists (fun x -> x.Name = "HCTZ") @>

[<Fact>]
let ``Delete removes the medication with the matching Id`` () =
  let repo = StubRepository([ medication 1 1 "HCTZ" ]) :> IMedicationRepository
  repo.Delete 1 1
  test <@ repo.GetAll(1) |> List.isEmpty @>

[<Fact>]
let ``Delete does not affect a medication belonging to a different member`` () =
  let repo = StubRepository([ medication 1 1 "HCTZ" ]) :> IMedicationRepository
  repo.Delete 2 1
  test <@ repo.GetAll(1) |> List.length = 1 @>

[<Fact>]
let ``Delete of a non-existent medication is a no-op`` () =
  let repo = StubRepository([]) :> IMedicationRepository
  repo.Delete 1 99
  test <@ repo.GetAll(1) |> List.isEmpty @>
