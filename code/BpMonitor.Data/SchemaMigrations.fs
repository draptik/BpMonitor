namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore
open BpMonitor.Core

module SchemaMigrations =
  let private withConn (ctx: BpMonitorDbContext) (f: System.Data.IDbConnection -> 'a) : 'a =
    let conn = ctx.Database.GetDbConnection()
    let wasOpen = conn.State = System.Data.ConnectionState.Open

    if not wasOpen then
      conn.Open()

    try
      f conn
    finally
      if not wasOpen then
        conn.Close()

  let private scalarInt64 (conn: System.Data.IDbConnection) (sql: string) : int64 =
    use cmd = conn.CreateCommand()
    cmd.CommandText <- sql
    cmd.ExecuteScalar() :?> int64

  let private columnExists (ctx: BpMonitorDbContext) (table: string) (column: string) =
    withConn ctx (fun conn ->
      let count =
        scalarInt64 conn $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'"

      count > 0L)

  let private addColumnIfMissing
    (ctx: BpMonitorDbContext)
    (table: string)
    (column: string)
    (colType: string)
    (defaultValue: string)
    =
    if not (columnExists ctx table column) then
      ctx.Database.ExecuteSqlRaw(
        $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {colType} NOT NULL DEFAULT '{defaultValue}'"
      )
      |> ignore

  /// Creates the Members table if it does not already exist.
  /// EnsureCreated() handles fresh databases, but existing databases that predate
  /// the Members entity need this explicit DDL.
  let private createMembersTableIfMissing (ctx: BpMonitorDbContext) =
    let goal = GoalRange.defaults

    ctx.Database.ExecuteSqlRaw(
      $"""CREATE TABLE IF NOT EXISTS "Members" (
        "Id" INTEGER NOT NULL CONSTRAINT "PK_Members" PRIMARY KEY AUTOINCREMENT,
        "Name" TEXT NOT NULL,
        "IsAdmin" INTEGER NOT NULL DEFAULT 0,
        "IsActive" INTEGER NOT NULL DEFAULT 1,
        "PasswordHash" TEXT NOT NULL DEFAULT '',
        "SystolicGoalMin" INTEGER NOT NULL DEFAULT {goal.SystolicMin},
        "SystolicGoalMax" INTEGER NOT NULL DEFAULT {goal.SystolicMax},
        "DiastolicGoalMin" INTEGER NOT NULL DEFAULT {goal.DiastolicMin},
        "DiastolicGoalMax" INTEGER NOT NULL DEFAULT {goal.DiastolicMax},
        "CreatedAt" TEXT NOT NULL,
        "ModifiedAt" TEXT NOT NULL
      )"""
    )
    |> ignore

  /// Ensures a default family member exists. Returns its Id, which is used as the
  /// default value when backfilling the MemberId column on existing readings.
  let private ensureDefaultMember (ctx: BpMonitorDbContext) : int =
    let count =
      withConn ctx (fun conn -> scalarInt64 conn "SELECT COUNT(*) FROM \"Members\"")

    if count = 0L then
      let now = System.DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss zzz")
      let goal = GoalRange.defaults

      ctx.Database.ExecuteSqlRaw(
        $"INSERT INTO \"Members\" (\"Name\", \"IsAdmin\", \"IsActive\", \"PasswordHash\", \"SystolicGoalMin\", \"SystolicGoalMax\", \"DiastolicGoalMin\", \"DiastolicGoalMax\", \"CreatedAt\", \"ModifiedAt\") VALUES ('Me', 1, 1, '', {goal.SystolicMin}, {goal.SystolicMax}, {goal.DiastolicMin}, {goal.DiastolicMax}, '{now}', '{now}')"
      )
      |> ignore

      withConn ctx (fun conn -> scalarInt64 conn "SELECT last_insert_rowid()") |> int
    else
      withConn ctx (fun conn -> scalarInt64 conn "SELECT \"Id\" FROM \"Members\" ORDER BY \"Id\" LIMIT 1")
      |> int

  /// Promotes the lowest-Id member to admin+active when no active admin exists.
  /// Protects databases seeded before IsAdmin/IsActive columns were added — those
  /// members received IsAdmin=0 (the column default) and would otherwise violate the
  /// invariant.
  let private ensureActiveAdmin (ctx: BpMonitorDbContext) =
    let count =
      withConn ctx (fun conn ->
        scalarInt64 conn "SELECT COUNT(*) FROM \"Members\" WHERE \"IsAdmin\" = 1 AND \"IsActive\" = 1")

    if count = 0L then
      ctx.Database.ExecuteSqlRaw(
        "UPDATE \"Members\" SET \"IsAdmin\" = 1, \"IsActive\" = 1 WHERE \"Id\" = (SELECT MIN(\"Id\") FROM \"Members\")"
      )
      |> ignore

  let apply (ctx: BpMonitorDbContext) =
    ctx.Database.EnsureCreated() |> ignore

    // Enable WAL mode for better read/write concurrency under light multi-user load.
    ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL") |> ignore
    ctx.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000") |> ignore

    addColumnIfMissing ctx "Readings" "CreatedAt" "TEXT" "0001-01-01 00:00:00 +00:00"
    addColumnIfMissing ctx "Readings" "ModifiedAt" "TEXT" "0001-01-01 00:00:00 +00:00"

    // Create the Members table (no-op on fresh DBs where EnsureCreated already created it).
    createMembersTableIfMissing ctx

    // Add IsAdmin/IsActive to existing Members tables that predate these columns.
    addColumnIfMissing ctx "Members" "IsAdmin" "INTEGER" "0"
    addColumnIfMissing ctx "Members" "IsActive" "INTEGER" "1"
    // Add PasswordHash for per-member login (empty string = unclaimed account).
    addColumnIfMissing ctx "Members" "PasswordHash" "TEXT" ""
    // Add goal-range columns, preset to the paper's recommended range (GoalRange.defaults).
    let goal = GoalRange.defaults
    addColumnIfMissing ctx "Members" "SystolicGoalMin" "INTEGER" (string goal.SystolicMin)
    addColumnIfMissing ctx "Members" "SystolicGoalMax" "INTEGER" (string goal.SystolicMax)
    addColumnIfMissing ctx "Members" "DiastolicGoalMin" "INTEGER" (string goal.DiastolicMin)
    addColumnIfMissing ctx "Members" "DiastolicGoalMax" "INTEGER" (string goal.DiastolicMax)

    // Ensure at least one default member exists and get its Id.
    let defaultMemberId = ensureDefaultMember ctx

    // Backfill existing readings with the default member Id.
    addColumnIfMissing ctx "Readings" "MemberId" "INTEGER" (string defaultMemberId)

    // Promote the lowest-Id member to admin+active when no active admin exists yet.
    ensureActiveAdmin ctx

    // Index for member-scoped reading queries (every GetAll/Update filters by MemberId).
    ctx.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"idx_readings_memberid\" ON \"Readings\" (\"MemberId\")")
    |> ignore
