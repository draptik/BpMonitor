module FileHelpersTests

open System.IO
open BpMonitor.Export.FileHelpers

open Swensen.Unquote
open Xunit

[<Fact>]
let ``tryWriteString returns Error instead of throwing when path is null`` () =
  let result = tryWriteString null "content"

  test <@ result <> Ok() @>

[<Fact>]
let ``tryWriteString returns Error instead of throwing when path is empty`` () =
  let result = tryWriteString "" "content"

  test <@ result <> Ok() @>

[<Fact>]
let ``tryWriteString overwrites existing content rather than appending`` () =
  let path = Path.GetTempFileName()
  tryWriteString path "first" |> ignore
  tryWriteString path "second" |> ignore

  let content = File.ReadAllText(path)

  test <@ content = "second" @>
