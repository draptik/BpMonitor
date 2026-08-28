module FileHelpersTests

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
