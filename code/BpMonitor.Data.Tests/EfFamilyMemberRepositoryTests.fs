module EfFamilyMemberRepositoryTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.Data

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

let private createRepo (ctx: BpMonitorDbContext) : IFamilyMemberRepository =
  EfFamilyMemberRepository(ctx, TimeProvider.System) :> IFamilyMemberRepository

let private newMember name isAdmin =
  FamilyMember.create name isAdmin
  |> Result.defaultWith (fun _ -> failwith "invalid member")

[<Fact>]
let ``GetAll returns empty list when database is empty`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  test <@ repo.GetAll() = [] @>

[<Fact>]
let ``GetAll returns all members`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  repo.Add(newMember "Alice" true) |> ignore
  repo.Add(newMember "Bob" false) |> ignore
  test <@ repo.GetAll().Length = 2 @>

[<Fact>]
let ``GetById returns Some when member exists`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let added = repo.Add(newMember "Alice" true)
  test <@ repo.GetById(added.Id) = Some added @>

[<Fact>]
let ``GetById returns None when member does not exist`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  test <@ repo.GetById(999) = None @>

[<Fact>]
let ``Add persists a member and assigns a non-zero Id`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let added = repo.Add(newMember "Alice" true)
  test <@ added.Id > 0 @>
  test <@ repo.GetAll().Length = 1 @>

[<Fact>]
let ``Add maps empty PasswordHash to None`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let added = repo.Add(newMember "Alice" true)
  test <@ added.PasswordHash = None @>

[<Fact>]
let ``Add maps non-empty PasswordHash to Some`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let m =
    { newMember "Alice" true with
        PasswordHash = Some "hashed" }

  let added = repo.Add m
  test <@ added.PasswordHash = Some "hashed" @>

[<Fact>]
let ``Add sets CreatedAt and ModifiedAt to current time`` () =
  let now = DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero)
  let timeProvider = FakeTimeProvider(now)
  use ctx = createContext ()
  let repo = EfFamilyMemberRepository(ctx, timeProvider) :> IFamilyMemberRepository
  let added = repo.Add(newMember "Alice" true)
  test <@ added.CreatedAt = now @>
  test <@ added.ModifiedAt = now @>

[<Fact>]
let ``Update modifies the stored member`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let added = repo.Add(newMember "Alice" true)
  repo.Update { added with Name = "Alicia" }
  test <@ (repo.GetById added.Id).Value.Name = "Alicia" @>

[<Fact>]
let ``Update sets ModifiedAt to current time and preserves CreatedAt`` () =
  let createdAt = DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
  let updatedAt = DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero)
  let timeProvider = FakeTimeProvider(createdAt)
  use ctx = createContext ()
  let repo = EfFamilyMemberRepository(ctx, timeProvider) :> IFamilyMemberRepository
  let added = repo.Add(newMember "Alice" true)
  timeProvider.SetUtcNow(updatedAt)
  repo.Update { added with Name = "Alicia" }
  let result = (repo.GetById added.Id).Value
  test <@ result.CreatedAt = createdAt @>
  test <@ result.ModifiedAt = updatedAt @>

[<Fact>]
let ``Update of a non-existent member is a no-op`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let ghost = newMember "Ghost" false
  let ghostWithId = { ghost with Id = 999 }
  repo.Update(ghostWithId)
  test <@ repo.GetAll() = [] @>

[<Fact>]
let ``Add persists a custom goal range and Update round-trips changes to it`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let goal =
    { SystolicMin = 100
      SystolicMax = 130
      DiastolicMin = 65
      DiastolicMax = 85 }

  let added =
    repo.Add
      { newMember "Alice" true with
          Goal = goal }

  test <@ added.Goal = goal @>
  test <@ (repo.GetById added.Id).Value.Goal = goal @>

  let newGoal =
    { SystolicMin = 95
      SystolicMax = 135
      DiastolicMin = 62
      DiastolicMax = 88 }

  repo.Update { added with Goal = newGoal }
  test <@ (repo.GetById added.Id).Value.Goal = newGoal @>

[<Fact>]
let ``GetById translates Id filter to SQL WHERE clause`` () =
  let log = ResizeArray<string>()
  use ctx = createContextWithLog log
  let repo = createRepo ctx
  let added = repo.Add(newMember "Alice" true)
  log.Clear()
  repo.GetById(added.Id) |> ignore

  let selectSql =
    log |> Seq.filter (fun s -> s.Contains("SELECT")) |> String.concat " "

  Assert.Contains("WHERE", selectSql)
