module EfTestContext

open System
open Xunit
open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore
open BpMonitor.Data

// EF never closes a connection passed in from outside, so the test context owns it.
type private ConnectionOwningContext(options, connection: SqliteConnection) =
  inherit BpMonitorDbContext(options)

  override _.Dispose() =
    base.Dispose()
    connection.Dispose()

/// Opens a fresh in-memory database whose context closes the connection on Dispose.
/// Doesn't create the schema, so SchemaMigrations tests can drive setup from scratch.
let createRawContext
  (configure: DbContextOptionsBuilder<BpMonitorDbContext> -> DbContextOptionsBuilder<BpMonitorDbContext>)
  =
  let connection = new SqliteConnection("DataSource=:memory:")
  connection.Open()

  let options =
    (DbContextOptionsBuilder<BpMonitorDbContext>().UseSqlite(connection) |> configure).Options

  new ConnectionOwningContext(options, connection) :> BpMonitorDbContext

let private createWith configure =
  let ctx = createRawContext configure
  ctx.Database.EnsureCreated() |> ignore
  ctx

let createContext () = createWith id

let createContextWithLog (log: ResizeArray<string>) =
  createWith _.LogTo(Action<string>(log.Add))

/// Asserts that the most recent SELECT logged via createContextWithLog included a WHERE clause,
/// i.e. that the repository pushed its filter down to SQL instead of filtering in memory.
let assertWhereClauseUsed (log: ResizeArray<string>) =
  let selectSql = log |> Seq.filter _.Contains("SELECT") |> String.concat " "
  Assert.Contains("WHERE", selectSql)
