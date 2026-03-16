namespace BpMonitor.Data

open Microsoft.EntityFrameworkCore

module SchemaMigrations =
  let private columnExists (ctx: BpMonitorDbContext) (table: string) (column: string) =
    let sql =
      $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}'"

    let conn = ctx.Database.GetDbConnection()
    conn.Open()

    try
      use cmd = conn.CreateCommand()
      cmd.CommandText <- sql
      let count = cmd.ExecuteScalar() :?> int64
      count > 0L
    finally
      conn.Close()

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

  let apply (ctx: BpMonitorDbContext) =
    ctx.Database.EnsureCreated() |> ignore
    addColumnIfMissing ctx "Readings" "CreatedAt" "TEXT" "0001-01-01 00:00:00 +00:00"
    addColumnIfMissing ctx "Readings" "ModifiedAt" "TEXT" "0001-01-01 00:00:00 +00:00"
