module ReadingRepositoryContractTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core

type private StubRepository(initial: BloodPressureReading list) =
  let readings = ResizeArray<BloodPressureReading>(initial)

  interface IReadingRepository with
    member _.GetAll() = readings |> Seq.toList
    member _.Add(r) = readings.Add(r)
    member _.AddMany(rs) = rs |> List.iter readings.Add

    member _.Update(r) =
      let idx = readings |> Seq.findIndex (fun x -> x.Id = r.Id)
      readings[idx] <- r

let private reading id sys dia hr =
  { Id = id
    Systolic = sys
    Diastolic = dia
    HeartRate = hr
    Timestamp = Timestamp.utc 2026 1 1 9 0 0
    Comments = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

[<Fact>]
let ``Update replaces the reading with the matching Id`` () =
  let repo = StubRepository([ reading 1 120 80 70 ]) :> IReadingRepository

  let updated =
    { reading 1 135 88 75 with
        Comments = Some "updated" }

  repo.Update(updated)
  test <@ repo.GetAll() |> List.exists (fun r -> r.Systolic = 135) @>

[<Fact>]
let ``Update does not affect other readings`` () =
  let repo =
    StubRepository([ reading 1 120 80 70; reading 2 130 85 72 ]) :> IReadingRepository

  repo.Update({ reading 1 135 88 75 with Id = 1 })
  test <@ repo.GetAll() |> List.exists (fun r -> r.Id = 2 && r.Systolic = 130) @>

[<Fact>]
let ``Update preserves the Id`` () =
  let repo = StubRepository([ reading 1 120 80 70 ]) :> IReadingRepository
  let updated = reading 1 135 88 75
  repo.Update(updated)
  test <@ repo.GetAll() |> List.forall (fun r -> r.Id = 1) @>

[<Fact>]
let ``Update reflects changes in subsequent GetAll`` () =
  let repo = StubRepository([ reading 1 120 80 70 ]) :> IReadingRepository

  repo.Update(
    { reading 1 120 80 70 with
        Comments = Some "after update" }
  )

  test <@ repo.GetAll().[0].Comments = Some "after update" @>
