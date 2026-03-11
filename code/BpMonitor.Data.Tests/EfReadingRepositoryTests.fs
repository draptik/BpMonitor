module EfReadingRepositoryTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Data

let private sample: BloodPressureReading =
  { Id = 0
    Systolic = 120
    Diastolic = 80
    HeartRate = 70
    Timestamp = DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
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

[<Fact>]
let ``GetAll returns empty list when database is empty`` () =
  use ctx = createContext ()

  let repo =
    EfReadingRepository(ctx, System.TimeProvider.System) :> IReadingRepository

  test <@ repo.GetAll() = [] @>

[<Fact>]
let ``Add persists a reading`` () =
  use ctx = createContext ()

  let repo =
    EfReadingRepository(ctx, System.TimeProvider.System) :> IReadingRepository

  repo.Add(sample)
  test <@ repo.GetAll().Length = 1 @>

[<Fact>]
let ``Add assigns a non-zero Id`` () =
  use ctx = createContext ()

  let repo =
    EfReadingRepository(ctx, System.TimeProvider.System) :> IReadingRepository

  repo.Add(sample)
  test <@ repo.GetAll().[0].Id > 0 @>

[<Fact>]
let ``Add preserves Comments when present`` () =
  use ctx = createContext ()

  let repo =
    EfReadingRepository(ctx, System.TimeProvider.System) :> IReadingRepository

  repo.Add(
    { sample with
        Comments = Some "test note" }
  )

  test <@ repo.GetAll().[0].Comments = Some "test note" @>

[<Fact>]
let ``Add preserves Comments as None when absent`` () =
  use ctx = createContext ()

  let repo =
    EfReadingRepository(ctx, System.TimeProvider.System) :> IReadingRepository

  repo.Add(sample)
  test <@ repo.GetAll().[0].Comments = None @>

[<Fact>]
let ``Add sets CreatedAt and ModifiedAt to current time`` () =
  let now = DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero)
  let timeProvider = FakeTimeProvider(now)
  use ctx = createContext ()
  let repo = EfReadingRepository(ctx, timeProvider) :> IReadingRepository
  repo.Add(sample)
  let result = repo.GetAll().[0]
  test <@ result.CreatedAt = now @>
  test <@ result.ModifiedAt = now @>

[<Fact>]
let ``Update preserves CreatedAt and sets ModifiedAt to current time`` () =
  let createdAt = DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
  let updatedAt = DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero)
  let timeProvider = FakeTimeProvider(createdAt)
  use ctx = createContext ()
  let repo = EfReadingRepository(ctx, timeProvider) :> IReadingRepository
  repo.Add(sample)
  let added = repo.GetAll().[0]
  timeProvider.SetUtcNow(updatedAt)
  repo.Update({ added with Systolic = 130 })
  let result = repo.GetAll().[0]
  test <@ result.CreatedAt = createdAt @>
  test <@ result.ModifiedAt = updatedAt @>
