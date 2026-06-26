module ReadingRepositoryContractTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open TestBuilders

type private StubRepository(initial: BloodPressureReading list) =
  let readings = ResizeArray<BloodPressureReading>(initial)

  interface IReadingRepository with
    member _.GetAll(memberId) =
      readings |> Seq.filter (fun r -> r.MemberId = memberId) |> Seq.toList

    member _.Add memberId r =
      readings.Add({ r with MemberId = memberId })

    member _.AddMany memberId rs =
      rs |> List.iter (fun r -> readings.Add({ r with MemberId = memberId }))

    member _.Update(r) =
      let idx =
        readings |> Seq.tryFindIndex (fun x -> x.Id = r.Id && x.MemberId = r.MemberId)

      match idx with
      | Some i -> readings[i] <- r
      | None -> ()

let private reading id memberId sys dia hr =
  mkReading id memberId sys dia hr (Timestamp.utc 2026 1 1 9 0 0)

[<Fact>]
let ``Update replaces the reading with the matching Id`` () =
  let repo = StubRepository([ reading 1 1 120 80 70 ]) :> IReadingRepository

  let updated =
    { reading 1 1 135 88 75 with
        Comments = Some "updated" }

  repo.Update(updated)
  test <@ repo.GetAll(1) |> List.exists (fun r -> r.Systolic = 135) @>

[<Fact>]
let ``Update does not affect other readings`` () =
  let repo =
    StubRepository([ reading 1 1 120 80 70; reading 2 1 130 85 72 ]) :> IReadingRepository

  repo.Update({ reading 1 1 135 88 75 with Id = 1 })
  test <@ repo.GetAll(1) |> List.exists (fun r -> r.Id = 2 && r.Systolic = 130) @>

[<Fact>]
let ``Update preserves the Id`` () =
  let repo = StubRepository([ reading 1 1 120 80 70 ]) :> IReadingRepository
  let updated = reading 1 1 135 88 75
  repo.Update(updated)
  test <@ repo.GetAll(1) |> List.forall (fun r -> r.Id = 1) @>

[<Fact>]
let ``Update reflects changes in subsequent GetAll`` () =
  let repo = StubRepository([ reading 1 1 120 80 70 ]) :> IReadingRepository

  repo.Update(
    { reading 1 1 120 80 70 with
        Comments = Some "after update" }
  )

  test <@ repo.GetAll(1).[0].Comments = Some "after update" @>

[<Fact>]
let ``GetAll returns only readings for the requested member`` () =
  let r1 = reading 1 1 120 80 70
  let r2 = reading 2 2 130 85 72
  let repo = StubRepository([ r1; r2 ]) :> IReadingRepository
  test <@ repo.GetAll(1) |> List.length = 1 @>
  test <@ repo.GetAll(1).[0].Id = 1 @>
  test <@ repo.GetAll(2).[0].Id = 2 @>

[<Fact>]
let ``Add stamps the reading with the given memberId`` () =
  let repo = StubRepository([]) :> IReadingRepository
  repo.Add 2 (reading 0 0 120 80 70)
  test <@ repo.GetAll(2) |> List.length = 1 @>
  test <@ repo.GetAll(1) |> List.isEmpty @>

[<Fact>]
let ``Update does not affect a reading belonging to a different member`` () =
  let r = reading 1 1 120 80 70
  let repo = StubRepository([ r ]) :> IReadingRepository
  // Attempt to update reading 1 as if it belonged to member 2 — should be a no-op
  repo.Update({ reading 1 2 135 90 75 with Id = 1 })
  test <@ repo.GetAll(1) |> List.exists (fun x -> x.Systolic = 120) @>

[<Fact>]
let ``Update of a non-existent reading is a no-op`` () =
  let repo = StubRepository([]) :> IReadingRepository
  repo.Update(reading 99 1 120 80 70)
  test <@ repo.GetAll(1) |> List.isEmpty @>
