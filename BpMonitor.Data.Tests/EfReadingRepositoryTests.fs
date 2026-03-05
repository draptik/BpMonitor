module EfReadingRepositoryTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore
open BpMonitor.Core
open BpMonitor.Data

let private sample : BloodPressureReading = {
    Id = 0; Systolic = 120; Diastolic = 80; HeartRate = 70
    Timestamp = DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
    Comments = None
}

let private createContext () =
    let connection = new SqliteConnection("DataSource=:memory:")
    connection.Open()
    let options =
        DbContextOptionsBuilder<BpMonitorDbContext>()
            .UseSqlite(connection)
            .Options
    let ctx = new BpMonitorDbContext(options)
    ctx.Database.EnsureCreated() |> ignore
    ctx

[<Fact>]
let ``GetAll returns empty list when database is empty`` () =
    use ctx = createContext()
    let repo = EfReadingRepository(ctx) :> IReadingRepository
    test <@ repo.GetAll() = [] @>

[<Fact>]
let ``Add persists a reading`` () =
    use ctx = createContext()
    let repo = EfReadingRepository(ctx) :> IReadingRepository
    repo.Add(sample)
    test <@ repo.GetAll().Length = 1 @>

[<Fact>]
let ``Add assigns a non-zero Id`` () =
    use ctx = createContext()
    let repo = EfReadingRepository(ctx) :> IReadingRepository
    repo.Add(sample)
    test <@ repo.GetAll().[0].Id > 0 @>

[<Fact>]
let ``Add preserves Comments when present`` () =
    use ctx = createContext()
    let repo = EfReadingRepository(ctx) :> IReadingRepository
    repo.Add({ sample with Comments = Some "test note" })
    test <@ repo.GetAll().[0].Comments = Some "test note" @>

[<Fact>]
let ``Add preserves Comments as None when absent`` () =
    use ctx = createContext()
    let repo = EfReadingRepository(ctx) :> IReadingRepository
    repo.Add(sample)
    test <@ repo.GetAll().[0].Comments = None @>
