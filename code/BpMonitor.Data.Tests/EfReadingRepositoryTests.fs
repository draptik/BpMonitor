module EfReadingRepositoryTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Time.Testing
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

let private createContext () =
  let connection = new SqliteConnection("DataSource=:memory:")
  connection.Open()

  let options =
    DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite(connection).Options

  let ctx = new BpMonitorDbContext(options)
  ctx.Database.EnsureCreated() |> ignore
  ctx

let private createRepo (ctx: BpMonitorDbContext) : IReadingRepository =
  EfReadingRepository(ctx, System.TimeProvider.System) :> IReadingRepository

[<Fact>]
let ``GetAll returns empty list when database is empty`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  test <@ repo.GetAll(defaultMemberId) = [] @>

[<Fact>]
let ``Add persists a reading`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  repo.Add defaultMemberId sample
  test <@ repo.GetAll(defaultMemberId).Length = 1 @>

[<Fact>]
let ``Add assigns a non-zero Id`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  repo.Add defaultMemberId sample
  test <@ repo.GetAll(defaultMemberId).[0].Id > 0 @>

[<Fact>]
let ``Add stamps the reading with the given memberId`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  repo.Add defaultMemberId sample
  test <@ repo.GetAll(defaultMemberId).[0].MemberId = defaultMemberId @>

[<Fact>]
let ``GetAll returns only readings for the requested member`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  repo.Add 1 sample

  repo.Add
    2
    { sample with
        Timestamp = Timestamp.utc 2026 1 2 9 0 0 }

  test <@ repo.GetAll(1).Length = 1 @>
  test <@ repo.GetAll(2).Length = 1 @>
  test <@ repo.GetAll(1).[0].MemberId = 1 @>

[<Fact>]
let ``Add preserves Comments when present`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  repo.Add
    defaultMemberId
    { sample with
        Comments = Some "test note" }

  test <@ repo.GetAll(defaultMemberId).[0].Comments = Some "test note" @>

[<Fact>]
let ``Add preserves Comments as None when absent`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  repo.Add defaultMemberId sample
  test <@ repo.GetAll(defaultMemberId).[0].Comments = None @>

[<Fact>]
let ``Add sets CreatedAt and ModifiedAt to current time`` () =
  let now = Timestamp.utc 2026 3 11 10 0 0
  let timeProvider = FakeTimeProvider(now)
  use ctx = createContext ()
  let repo = EfReadingRepository(ctx, timeProvider) :> IReadingRepository
  repo.Add defaultMemberId sample
  let result = repo.GetAll(defaultMemberId)[0]
  test <@ result.CreatedAt = now @>
  test <@ result.ModifiedAt = now @>

[<Fact>]
let ``AddMany persists all readings`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let second =
    { sample with
        Timestamp = Timestamp.utc 2026 1 2 9 0 0 }

  repo.AddMany defaultMemberId [ sample; second ]
  test <@ repo.GetAll(defaultMemberId).Length = 2 @>

[<Fact>]
let ``AddMany sets CreatedAt and ModifiedAt to current time`` () =
  let now = Timestamp.utc 2026 3 11 10 0 0
  let timeProvider = FakeTimeProvider(now)
  use ctx = createContext ()
  let repo = EfReadingRepository(ctx, timeProvider) :> IReadingRepository

  let second =
    { sample with
        Timestamp = Timestamp.utc 2026 1 2 9 0 0 }

  repo.AddMany defaultMemberId [ sample; second ]
  let readings = repo.GetAll(defaultMemberId)
  test <@ readings[0].CreatedAt = now @>
  test <@ readings[0].ModifiedAt = now @>
  test <@ readings[1].CreatedAt = now @>
  test <@ readings[1].ModifiedAt = now @>

[<Fact>]
let ``Update preserves CreatedAt and sets ModifiedAt to current time`` () =
  let createdAt = Timestamp.utc 2026 1 1 9 0 0
  let updatedAt = Timestamp.utc 2026 3 11 10 0 0
  let timeProvider = FakeTimeProvider(createdAt)
  use ctx = createContext ()
  let repo = EfReadingRepository(ctx, timeProvider) :> IReadingRepository
  repo.Add defaultMemberId sample
  let added = repo.GetAll(defaultMemberId)[0]
  timeProvider.SetUtcNow(updatedAt)
  repo.Update({ added with Systolic = 130 })
  let result = repo.GetAll(defaultMemberId)[0]
  test <@ result.CreatedAt = createdAt @>
  test <@ result.ModifiedAt = updatedAt @>

[<Fact>]
let ``Update does not affect a reading belonging to a different member`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  repo.Add 1 sample
  let added = repo.GetAll(1)[0]
  // Attempt to update as member 2 — MemberId guard should prevent it
  repo.Update(
    { added with
        Systolic = 999
        MemberId = 2 }
  )
  // Reading should remain unchanged for member 1
  test <@ (repo.GetAll 1).[0].Systolic = 120 @>
