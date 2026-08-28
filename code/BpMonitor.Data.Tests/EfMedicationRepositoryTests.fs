module EfMedicationRepositoryTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Time.Testing
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

let private createContext () =
  let connection = new SqliteConnection("DataSource=:memory:")
  connection.Open()

  let options =
    DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite(connection).Options

  let ctx = new BpMonitorDbContext(options)
  ctx.Database.EnsureCreated() |> ignore
  ctx

let private createContextWithLog (log: ResizeArray<string>) =
  let connection = new SqliteConnection("DataSource=:memory:")
  connection.Open()

  let options =
    DbContextOptionsBuilder<BpMonitorDbContext>()
      .UseSqlite(connection)
      .LogTo(System.Action<string>(fun s -> log.Add(s)))
      .Options

  let ctx = new BpMonitorDbContext(options)
  ctx.Database.EnsureCreated() |> ignore
  ctx

let private createRepo (ctx: BpMonitorDbContext) : IMedicationRepository =
  EfMedicationRepository(ctx, TimeProvider.System) :> IMedicationRepository

[<Fact>]
let ``GetAll returns empty list when database is empty`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  test <@ repo.GetAll(defaultMemberId) = [] @>

[<Fact>]
let ``Add persists a medication`` () =
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
let ``Add stamps the medication with the given memberId`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  repo.Add defaultMemberId sample
  test <@ repo.GetAll(defaultMemberId).[0].MemberId = defaultMemberId @>

[<Fact>]
let ``GetAll returns only medications for the requested member`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  repo.Add 1 sample
  repo.Add 2 { sample with Name = "lisinopril" }
  test <@ repo.GetAll(1).Length = 1 @>
  test <@ repo.GetAll(2).Length = 1 @>
  test <@ repo.GetAll(1).[0].MemberId = 1 @>

[<Fact>]
let ``Add preserves FullName when present`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  repo.Add defaultMemberId sample
  test <@ repo.GetAll(defaultMemberId).[0].FullName = Some "hydrochlorothiazide" @>

[<Fact>]
let ``Add preserves FullName as None when absent`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  repo.Add defaultMemberId { sample with FullName = None }
  test <@ repo.GetAll(defaultMemberId).[0].FullName = None @>

[<Fact>]
let ``Add preserves Comment when present`` () =
  use ctx = createContext ()
  let repo = createRepo ctx

  repo.Add
    defaultMemberId
    { sample with
        Comment = Some "Ran out of medication" }

  test <@ repo.GetAll(defaultMemberId).[0].Comment = Some "Ran out of medication" @>

[<Fact>]
let ``Add preserves Comment as None when absent`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  repo.Add defaultMemberId sample
  test <@ repo.GetAll(defaultMemberId).[0].Comment = None @>

[<Fact>]
let ``Add preserves EndDate when present`` () =
  use ctx = createContext ()
  let repo = createRepo ctx

  repo.Add
    defaultMemberId
    { sample with
        EndDate = Some(DateOnly(2026, 2, 1)) }

  test <@ repo.GetAll(defaultMemberId).[0].EndDate = Some(DateOnly(2026, 2, 1)) @>

[<Fact>]
let ``Add preserves EndDate as None when absent`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  repo.Add defaultMemberId sample
  test <@ repo.GetAll(defaultMemberId).[0].EndDate = None @>

[<Fact>]
let ``Add sets CreatedAt and ModifiedAt to current time`` () =
  let now = Timestamp.utc 2026 3 11 10 0 0
  let timeProvider = FakeTimeProvider(now)
  use ctx = createContext ()
  let repo = EfMedicationRepository(ctx, timeProvider) :> IMedicationRepository
  repo.Add defaultMemberId sample
  let result = repo.GetAll(defaultMemberId)[0]
  test <@ result.CreatedAt = now @>
  test <@ result.ModifiedAt = now @>

[<Fact>]
let ``Update preserves CreatedAt and sets ModifiedAt to current time`` () =
  let createdAt = Timestamp.utc 2026 1 1 9 0 0
  let updatedAt = Timestamp.utc 2026 3 11 10 0 0
  let timeProvider = FakeTimeProvider(createdAt)
  use ctx = createContext ()
  let repo = EfMedicationRepository(ctx, timeProvider) :> IMedicationRepository
  repo.Add defaultMemberId sample
  let added = repo.GetAll(defaultMemberId)[0]
  timeProvider.SetUtcNow(updatedAt)
  repo.Update({ added with Name = "HCTZ 25mg" })
  let result = repo.GetAll(defaultMemberId)[0]
  test <@ result.Name = "HCTZ 25mg" @>
  test <@ result.CreatedAt = createdAt @>
  test <@ result.ModifiedAt = updatedAt @>

[<Fact>]
let ``Update of a non-existent medication is a no-op`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  repo.Add defaultMemberId sample

  let ghost =
    { sample with
        Id = 999
        MemberId = defaultMemberId }

  repo.Update(ghost)
  test <@ repo.GetAll(defaultMemberId).Length = 1 @>

[<Fact>]
let ``Update does not affect a medication belonging to a different member`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  repo.Add 1 sample
  let added = repo.GetAll(1)[0]

  repo.Update(
    { added with
        Name = "renamed"
        MemberId = 2 }
  )

  test <@ (repo.GetAll 1).[0].Name = "HCTZ" @>

[<Fact>]
let ``Delete removes the medication`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  repo.Add defaultMemberId sample
  let added = repo.GetAll(defaultMemberId)[0]
  repo.Delete defaultMemberId added.Id
  test <@ repo.GetAll(defaultMemberId) = [] @>

[<Fact>]
let ``Delete does not affect a medication belonging to a different member`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  repo.Add 1 sample
  let added = repo.GetAll(1)[0]
  repo.Delete 2 added.Id
  test <@ repo.GetAll(1).Length = 1 @>

[<Fact>]
let ``GetAll translates MemberId filter to SQL WHERE clause`` () =
  let log = ResizeArray<string>()
  use ctx = createContextWithLog log
  let repo = createRepo ctx
  repo.Add defaultMemberId sample
  log.Clear()
  repo.GetAll(defaultMemberId) |> ignore

  let selectSql = log |> Seq.filter _.Contains("SELECT") |> String.concat " "

  Assert.Contains("WHERE", selectSql)
