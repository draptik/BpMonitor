namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore

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
    ctx.Database.ExecuteSqlRaw(
      """CREATE TABLE IF NOT EXISTS "Members" (
        "Id" INTEGER NOT NULL CONSTRAINT "PK_Members" PRIMARY KEY AUTOINCREMENT,
        "Name" TEXT NOT NULL,
        "CreatedAt" TEXT NOT NULL,
        "ModifiedAt" TEXT NOT NULL
      )"""
    )
    |> ignore

  /// Ensures a default family member exists. Returns its Id, which is used as the
  /// default value when back-filling the MemberId column on existing readings.
  let private ensureDefaultMember (ctx: BpMonitorDbContext) : int =
    let count =
      withConn ctx (fun conn -> scalarInt64 conn "SELECT COUNT(*) FROM \"Members\"")

    if count = 0L then
      ctx.Database.ExecuteSqlRaw(
        "INSERT INTO \"Members\" (\"Name\", \"CreatedAt\", \"ModifiedAt\") VALUES ('Me', '0001-01-01 00:00:00 +00:00', '0001-01-01 00:00:00 +00:00')"
      )
      |> ignore

      withConn ctx (fun conn -> scalarInt64 conn "SELECT last_insert_rowid()") |> int
    else
      withConn ctx (fun conn -> scalarInt64 conn "SELECT \"Id\" FROM \"Members\" ORDER BY \"Id\" LIMIT 1")
      |> int

  let apply (ctx: BpMonitorDbContext) =
    ctx.Database.EnsureCreated() |> ignore

    // Enable WAL mode for better read/write concurrency under light multi-user load.
    ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL") |> ignore
    ctx.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000") |> ignore

    addColumnIfMissing ctx "Readings" "CreatedAt" "TEXT" "0001-01-01 00:00:00 +00:00"
    addColumnIfMissing ctx "Readings" "ModifiedAt" "TEXT" "0001-01-01 00:00:00 +00:00"

    // Create the Members table (no-op on fresh DBs where EnsureCreated already created it).
    createMembersTableIfMissing ctx

    // Ensure at least one default member exists and get its Id.
    let defaultMemberId = ensureDefaultMember ctx

    // Back-fill existing readings with the default member Id.
    addColumnIfMissing ctx "Readings" "MemberId" "INTEGER" (string defaultMemberId)
