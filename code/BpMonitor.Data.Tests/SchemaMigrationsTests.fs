module SchemaMigrationsTests

open Xunit
open Swensen.Unquote
open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore
open BpMonitor.Data

/// Creates a fresh in-memory connection + context WITHOUT calling EnsureCreated,
/// so SchemaMigrations.apply can drive the full setup from scratch.
let private createRawContext () =
  let connection = new SqliteConnection("DataSource=:memory:")
  connection.Open()

  let options =
    DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite(connection).Options

  new BpMonitorDbContext(options)

let private scalarInt64 (conn: System.Data.IDbConnection) (sql: string) : int64 =
  use cmd = conn.CreateCommand()
  cmd.CommandText <- sql
  cmd.ExecuteScalar() :?> int64

[<Fact>]
let ``apply on a fresh database creates the Members table`` () =
  use ctx = createRawContext ()
  SchemaMigrations.apply ctx

  let conn = ctx.Database.GetDbConnection()

  let tableCount =
    scalarInt64 conn "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Members'"

  test <@ tableCount = 1L @>

[<Fact>]
let ``apply seeds a default member named Me with admin rights`` () =
  use ctx = createRawContext ()
  SchemaMigrations.apply ctx

  let members = ctx.Members |> Seq.toList
  test <@ members.Length = 1 @>
  test <@ members[0].Name = "Me" @>
  test <@ members[0].IsAdmin = true @>
  test <@ members[0].IsActive = true @>

[<Fact>]
let ``apply seeds the default member with the paper's preset goal range`` () =
  use ctx = createRawContext ()
  SchemaMigrations.apply ctx

  let m = ctx.Members |> Seq.exactlyOne
  test <@ m.SystolicGoalMin = 90 @>
  test <@ m.SystolicGoalMax = 140 @>
  test <@ m.DiastolicGoalMin = 60 @>
  test <@ m.DiastolicGoalMax = 90 @>

[<Fact>]
let ``apply adds goal-range columns with preset defaults to a legacy Members table`` () =
  let connection = new SqliteConnection("DataSource=:memory:")
  connection.Open()

  // Simulate a legacy DB: Members table predating the goal-range columns.
  use setupCmd = connection.CreateCommand()

  setupCmd.CommandText <-
    """CREATE TABLE "Members" (
         "Id" INTEGER NOT NULL CONSTRAINT "PK_Members" PRIMARY KEY AUTOINCREMENT,
         "Name" TEXT NOT NULL,
         "IsAdmin" INTEGER NOT NULL DEFAULT 0,
         "IsActive" INTEGER NOT NULL DEFAULT 1,
         "PasswordHash" TEXT NOT NULL DEFAULT '',
         "CreatedAt" TEXT NOT NULL,
         "ModifiedAt" TEXT NOT NULL
       )"""

  setupCmd.ExecuteNonQuery() |> ignore

  // apply() also back-fills the Readings table — it must exist for apply() to succeed,
  // even though this test only exercises the Members goal-range columns.
  use readingsCmd = connection.CreateCommand()

  readingsCmd.CommandText <-
    """CREATE TABLE "Readings" (
         "Id" INTEGER NOT NULL CONSTRAINT "PK_Readings" PRIMARY KEY AUTOINCREMENT,
         "Systolic" INTEGER NOT NULL,
         "Diastolic" INTEGER NOT NULL,
         "HeartRate" INTEGER NOT NULL,
         "Timestamp" TEXT NOT NULL,
         "Comments" TEXT
       )"""

  readingsCmd.ExecuteNonQuery() |> ignore

  use insertCmd = connection.CreateCommand()

  insertCmd.CommandText <-
    "INSERT INTO \"Members\" (\"Name\", \"IsAdmin\", \"IsActive\", \"CreatedAt\", \"ModifiedAt\") VALUES ('Me', 1, 1, '2026-01-01 09:00:00 +00:00', '2026-01-01 09:00:00 +00:00')"

  insertCmd.ExecuteNonQuery() |> ignore

  let options =
    DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite(connection).Options

  use ctx = new BpMonitorDbContext(options)
  SchemaMigrations.apply ctx

  let m = ctx.Members |> Seq.exactlyOne
  test <@ m.SystolicGoalMin = 90 @>
  test <@ m.SystolicGoalMax = 140 @>
  test <@ m.DiastolicGoalMin = 60 @>
  test <@ m.DiastolicGoalMax = 90 @>

[<Fact>]
let ``apply is idempotent - calling it twice does not create duplicate members`` () =
  use ctx = createRawContext ()
  SchemaMigrations.apply ctx
  SchemaMigrations.apply ctx

  let members = ctx.Members |> Seq.toList
  test <@ members.Length = 1 @>

[<Fact>]
let ``apply adds missing columns to an existing Readings table without MemberId`` () =
  let connection = new SqliteConnection("DataSource=:memory:")
  connection.Open()

  // Simulate a legacy DB: Readings table without MemberId, CreatedAt, or ModifiedAt.
  use setupCmd = connection.CreateCommand()

  setupCmd.CommandText <-
    """CREATE TABLE "Readings" (
         "Id" INTEGER NOT NULL CONSTRAINT "PK_Readings" PRIMARY KEY AUTOINCREMENT,
         "Systolic" INTEGER NOT NULL,
         "Diastolic" INTEGER NOT NULL,
         "HeartRate" INTEGER NOT NULL,
         "Timestamp" TEXT NOT NULL,
         "Comments" TEXT
       )"""

  setupCmd.ExecuteNonQuery() |> ignore

  use insertCmd = connection.CreateCommand()

  insertCmd.CommandText <-
    "-- noinspection SqlInsertValues
    INSERT INTO \"Readings\" (Systolic, Diastolic, HeartRate, Timestamp) VALUES (120, 80, 70, '2026-01-01 09:00:00 +00:00')"

  insertCmd.ExecuteNonQuery() |> ignore

  let options =
    DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite(connection).Options

  use ctx = new BpMonitorDbContext(options)
  SchemaMigrations.apply ctx

  // After apply the reading should have MemberId set to the default member's Id (1).
  let memberId =
    scalarInt64 connection "SELECT \"MemberId\" FROM \"Readings\" LIMIT 1"

  test <@ memberId > 0L @>

[<Fact>]
let ``apply promotes lowest-Id member to active admin when no active admin exists`` () =
  use ctx = createRawContext ()
  SchemaMigrations.apply ctx

  // Demote the seeded member to non-admin via raw SQL (bypasses domain invariant).
  ctx.Database.ExecuteSqlRaw("UPDATE \"Members\" SET \"IsAdmin\" = 0") |> ignore

  // Verify there is no active admin now.
  let conn = ctx.Database.GetDbConnection()

  let adminCount =
    scalarInt64 conn "SELECT COUNT(*) FROM \"Members\" WHERE \"IsAdmin\" = 1 AND \"IsActive\" = 1"

  test <@ adminCount = 0L @>

  // Re-apply — ensureActiveAdmin should promote the lowest-Id member.
  SchemaMigrations.apply ctx

  let promoted = ctx.Members |> Seq.minBy _.Id
  test <@ promoted.IsAdmin = true @>
  test <@ promoted.IsActive = true @>
