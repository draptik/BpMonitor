module InMemoryReadingRepositoryTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Data

let private defaultMemberId = 1

let private sample: BloodPressureReading =
  {
    Id = 0
    MemberId = 0
    Systolic = 120
    Diastolic = 80
    HeartRate = 70
    Timestamp = Timestamp.utc 2026 1 1 9 0 0
    Comments = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue
  }

[<Fact>]
let ``GetAll returns default sample readings on startup`` () =
  let repo = InMemoryReadingRepository(None) :> IReadingRepository
  test <@ repo.GetAll(defaultMemberId).Length > 0 @>

[<Fact>]
let ``Add assigns sequential Ids starting at 1`` () =
  let repo = InMemoryReadingRepository(Some []) :> IReadingRepository
  repo.Add defaultMemberId sample
  repo.Add defaultMemberId sample
  let readings = repo.GetAll(defaultMemberId)
  test <@ readings[0].Id = 1 @>
  test <@ readings[1].Id = 2 @>

[<Fact>]
let ``AddMany persists all readings`` () =
  let repo = InMemoryReadingRepository(Some []) :> IReadingRepository

  let second =
    { sample with
        Timestamp = Timestamp.utc 2026 1 2 9 0 0
    }

  repo.AddMany defaultMemberId [ sample; second ]
  test <@ repo.GetAll(defaultMemberId).Length = 2 @>

[<Fact>]
let ``AddMany assigns sequential Ids`` () =
  let repo = InMemoryReadingRepository(Some []) :> IReadingRepository

  let second =
    { sample with
        Timestamp = Timestamp.utc 2026 1 2 9 0 0
    }

  repo.AddMany defaultMemberId [ sample; second ]
  let readings = repo.GetAll(defaultMemberId)
  test <@ readings[0].Id = 1 @>
  test <@ readings[1].Id = 2 @>
