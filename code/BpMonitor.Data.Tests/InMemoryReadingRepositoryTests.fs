module InMemoryReadingRepositoryTests

open System
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Data

let private defaultMemberId = 1

let private sample: BloodPressureReading =
  { Id = 0
    MemberId = 0
    Systolic = 120
    Diastolic = 80
    HeartRate = 70
    Timestamp = Timestamp.utc 2026 1 1 9 0 0
    Comments = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

[<Fact>]
let ``GetAll returns default sample readings on startup`` () =
  let repo = InMemoryReadingRepository(None) :> IReadingRepository
  test <@ repo.GetAll(defaultMemberId).Length > 0 @>

[<Fact>]
let ``Add makes reading available via GetAll`` () =
  let repo = InMemoryReadingRepository(Some []) :> IReadingRepository
  repo.Add defaultMemberId sample
  test <@ repo.GetAll(defaultMemberId).Length = 1 @>

[<Fact>]
let ``Add stamps the reading with the given memberId`` () =
  let repo = InMemoryReadingRepository(Some []) :> IReadingRepository
  repo.Add 2 sample
  test <@ repo.GetAll(2).Length = 1 @>
  test <@ repo.GetAll(2).[0].MemberId = 2 @>
  test <@ repo.GetAll(defaultMemberId).Length = 0 @>

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
        Timestamp = Timestamp.utc 2026 1 2 9 0 0 }

  repo.AddMany defaultMemberId [ sample; second ]
  test <@ repo.GetAll(defaultMemberId).Length = 2 @>

[<Fact>]
let ``AddMany assigns sequential Ids`` () =
  let repo = InMemoryReadingRepository(Some []) :> IReadingRepository

  let second =
    { sample with
        Timestamp = Timestamp.utc 2026 1 2 9 0 0 }

  repo.AddMany defaultMemberId [ sample; second ]
  let readings = repo.GetAll(defaultMemberId)
  test <@ readings[0].Id = 1 @>
  test <@ readings[1].Id = 2 @>

[<Fact>]
let ``GetAll returns only readings for the requested member`` () =
  let r1 = { sample with Id = 1; MemberId = 1 }

  let r2 =
    { sample with
        Id = 2
        MemberId = 2
        Timestamp = Timestamp.utc 2026 1 2 9 0 0 }

  let repo = InMemoryReadingRepository(Some [ r1; r2 ]) :> IReadingRepository
  test <@ repo.GetAll(1).Length = 1 @>
  test <@ repo.GetAll(2).Length = 1 @>
  test <@ repo.GetAll(1).[0].Id = 1 @>
