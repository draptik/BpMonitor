module EfFamilyMemberRepositoryTests

open System
open Xunit
open Swensen.Unquote
open Microsoft.Extensions.Time.Testing
open BpMonitor.Core
open BpMonitor.TestSupport.TestBuilders
open BpMonitor.Data
open Microsoft.EntityFrameworkCore

let private createContext = EfTestContext.createContext
let private createContextWithLog = EfTestContext.createContextWithLog

let private createRepo (ctx: BpMonitorDbContext) : IFamilyMemberRepository =
  EfFamilyMemberRepository(ctx, TimeProvider.System) :> IFamilyMemberRepository

[<Fact>]
let ``Add persists a member and assigns a non-zero Id`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let added = repo.Add(mkMember 0 "Alice" true true)
  test <@ added.Id > 0 @>
  test <@ repo.GetAll().Length = 1 @>

[<Fact>]
let ``Add maps empty PasswordHash to NoCredential`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let added = repo.Add(mkMember 0 "Alice" true true)
  test <@ added.Credential = NoCredential @>

[<Fact>]
let ``Add maps non-empty PasswordHash to Password`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let m =
    { mkMember 0 "Alice" true true with
        Credential = Password "hashed"
    }

  let added = repo.Add m
  test <@ added.Credential = Password "hashed" @>

[<Fact>]
let ``Add maps a setup code hash to PendingSetup`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let m =
    { mkMember 0 "Alice" true true with
        Credential = PendingSetup "codehash"
    }

  let added = repo.Add m
  test <@ added.Credential = PendingSetup "codehash" @>

// Rows written by older versions or by hand can hold values the app itself never stores.
[<Fact>]
let ``GetById maps a row with both a password hash and a setup code hash to Password`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  let added = repo.Add(mkMember 0 "Alice" true true)

  ctx.Database.ExecuteSqlRaw("UPDATE \"Members\" SET \"PasswordHash\" = 'hashed', \"SetupCodeHash\" = 'codehash'")
  |> ignore

  test <@ (repo.GetById added.Id).Value.Credential = Password "hashed" @>

[<Fact>]
let ``GetById falls back to English for an unsupported stored language code`` () =
  use ctx = createContext ()
  let repo = createRepo ctx
  let added = repo.Add(mkMember 0 "Alice" true true)

  ctx.Database.ExecuteSqlRaw("UPDATE \"Members\" SET \"Language\" = 'fr'")
  |> ignore

  test <@ (repo.GetById added.Id).Value.Language = English @>

[<Fact>]
let ``Update to Password clears the stored setup code hash`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let added =
    repo.Add
      { mkMember 0 "Alice" true true with
          Credential = PendingSetup "codehash"
      }

  repo.Update
    { added with
        Credential = Password "hashed"
    }

  let stored = ctx.Members.AsNoTracking() |> Seq.exactlyOne
  test <@ stored.SetupCodeHash = "" @>

[<Fact>]
let ``Add sets CreatedAt and ModifiedAt to current time`` () =
  let now = DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero)
  let timeProvider = FakeTimeProvider(now)
  use ctx = createContext ()
  let repo = EfFamilyMemberRepository(ctx, timeProvider) :> IFamilyMemberRepository
  let added = repo.Add(mkMember 0 "Alice" true true)
  test <@ added.CreatedAt = now @>
  test <@ added.ModifiedAt = now @>

[<Fact>]
let ``Update sets ModifiedAt to current time and preserves CreatedAt`` () =
  let createdAt = DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero)
  let updatedAt = DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero)
  let timeProvider = FakeTimeProvider(createdAt)
  use ctx = createContext ()
  let repo = EfFamilyMemberRepository(ctx, timeProvider) :> IFamilyMemberRepository
  let added = repo.Add(mkMember 0 "Alice" true true)
  timeProvider.SetUtcNow(updatedAt)
  repo.Update { added with Name = "Alicia" }
  let result = (repo.GetById added.Id).Value
  test <@ result.CreatedAt = createdAt @>
  test <@ result.ModifiedAt = updatedAt @>

[<Fact>]
let ``UpdateLanguage sets ModifiedAt to current time`` () =
  let updatedAt = DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero)

  let timeProvider =
    FakeTimeProvider(DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero))

  use ctx = createContext ()
  let repo = EfFamilyMemberRepository(ctx, timeProvider) :> IFamilyMemberRepository
  let added = repo.Add(mkMember 0 "Alice" true true)
  timeProvider.SetUtcNow(updatedAt)
  repo.UpdateLanguage added.Id German
  test <@ (repo.GetById added.Id).Value.ModifiedAt = updatedAt @>

[<Fact>]
let ``UpdateGoal sets ModifiedAt to current time`` () =
  let updatedAt = DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero)

  let timeProvider =
    FakeTimeProvider(DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero))

  use ctx = createContext ()
  let repo = EfFamilyMemberRepository(ctx, timeProvider) :> IFamilyMemberRepository
  let added = repo.Add(mkMember 0 "Alice" true true)
  timeProvider.SetUtcNow(updatedAt)
  repo.UpdateGoal added.Id GoalRange.defaults
  test <@ (repo.GetById added.Id).Value.ModifiedAt = updatedAt @>

[<Fact>]
let ``Add persists a custom goal range and Update round-trips changes to it`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let goal = mkGoal 100 130 65 85

  let added =
    repo.Add
      { mkMember 0 "Alice" true true with
          Goal = goal
      }

  test <@ added.Goal = goal @>
  test <@ (repo.GetById added.Id).Value.Goal = goal @>

  let newGoal = mkGoal 95 135 62 88

  repo.Update { added with Goal = newGoal }
  test <@ (repo.GetById added.Id).Value.Goal = newGoal @>

[<Fact>]
let ``Add persists Language and Update round-trips a changed Language`` () =
  use ctx = createContext ()

  let repo = createRepo ctx

  let added =
    repo.Add
      { mkMember 0 "Alice" true true with
          Language = German
      }

  test <@ added.Language = German @>
  test <@ (repo.GetById added.Id).Value.Language = German @>

  repo.Update { added with Language = English }
  test <@ (repo.GetById added.Id).Value.Language = English @>

[<Fact>]
let ``GetById translates Id filter to SQL WHERE clause`` () =
  let log = ResizeArray<string>()
  use ctx = createContextWithLog log
  let repo = createRepo ctx
  let added = repo.Add(mkMember 0 "Alice" true true)
  log.Clear()
  repo.GetById(added.Id) |> ignore

  EfTestContext.assertWhereClauseUsed log
