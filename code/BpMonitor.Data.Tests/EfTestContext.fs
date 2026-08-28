module EfTestContext

open Xunit
open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore
open BpMonitor.Data

let createContext () =
  let connection = new SqliteConnection("DataSource=:memory:")
  connection.Open()

  let options =
    DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite(connection).Options

  let ctx = new BpMonitorDbContext(options)
  ctx.Database.EnsureCreated() |> ignore
  ctx

let createContextWithLog (log: ResizeArray<string>) =
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

/// Asserts that the most recent SELECT logged via createContextWithLog included a WHERE clause,
/// i.e. that the repository pushed its filter down to SQL instead of filtering in memory.
let assertWhereClauseUsed (log: ResizeArray<string>) =
  let selectSql = log |> Seq.filter _.Contains("SELECT") |> String.concat " "
  Assert.Contains("WHERE", selectSql)
