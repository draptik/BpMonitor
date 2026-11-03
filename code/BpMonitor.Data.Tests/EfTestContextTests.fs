module EfTestContextTests

open System.Data
open Microsoft.EntityFrameworkCore
open Swensen.Unquote
open Xunit

[<Fact>]
let ``disposing a test context closes its in-memory SQLite connection`` () =
  let contexts =
    [
      EfTestContext.createContext ()
      EfTestContext.createContextWithLog (ResizeArray())
    ]

  let connections = contexts |> List.map _.Database.GetDbConnection()
  contexts |> List.iter _.Dispose()
  test <@ connections |> List.forall (fun c -> c.State = ConnectionState.Closed) @>
