module SetupCodeTests

open System.Text.RegularExpressions
open Xunit
open Swensen.Unquote
open BpMonitor.Core

[<Fact>]
let ``generate returns two groups of four characters without look-alikes`` () =
  let code = SetupCode.generate ()
  test <@ Regex.IsMatch(code, "^[ABCDEFGHJKMNPQRSTVWXYZ2-9]{4}-[ABCDEFGHJKMNPQRSTVWXYZ2-9]{4}$") @>

[<Fact>]
let ``generate returns a different code each time`` () =
  let codes = List.init 100 (fun _ -> SetupCode.generate ())
  test <@ List.distinct codes = codes @>

[<Fact>]
let ``verify accepts the code typed in lower case with a space instead of the dash`` () =
  let hash = SetupCode.hash "K7QX-M2PA"
  test <@ SetupCode.verify "k7qx m2pa" hash @>

[<Fact>]
let ``verify rejects a different code`` () =
  let hash = SetupCode.hash "K7QX-M2PA"
  test <@ not (SetupCode.verify "K7QX-M2PB" hash) @>
