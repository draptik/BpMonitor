module SetupCodeTests

open System.Text.RegularExpressions
open Xunit
open Swensen.Unquote
open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Core
open BpMonitor.TestSupport.TestBuilders

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

// A code from the setup-code alphabet and how it was typed: random case, dashes and spaces anywhere.
let private typedCodeGen =
  gen {
    let! chars = Gen.elements "ABCDEFGHJKMNPQRSTVWXYZ23456789" |> Gen.arrayOfLength 8
    let code = System.String chars

    let! typed =
      chars
      |> Gen.collectToArray (fun c ->
        gen {
          let! lower = Gen.elements [ true; false ]
          let! sep = Gen.elements [ ""; ""; "-"; " " ]

          return
            sep
            + (if lower then
                 string (System.Char.ToLowerInvariant c)
               else
                 string c)
        })

    let! trailing = Gen.elements [ ""; "-"; " " ]
    return code, String.concat "" typed + trailing
  }

// Each case runs PBKDF2 twice, so keep the run short.
[<Property(MaxTest = 20)>]
let ``verify accepts any code typed in any case with dashes or spaces anywhere`` () =
  Prop.forAll (Arb.fromGen typedCodeGen) (fun (code, typed) -> SetupCode.verify typed (SetupCode.hash code))

[<Fact>]
let ``issueMissing issues codes only to members without a credential`` () =
  let members =
    [
      mkMember 1 "Me" true true
      { mkMember 2 "Bart" false true with
          Credential = Password "hashed"
      }
      { mkMember 3 "Lisa" false true with
          Credential = PendingSetup "codehash"
      }
    ]

  let issuedIds = SetupCode.issueMissing members |> List.map (fun (m, _) -> m.Id)
  test <@ issuedIds = [ 1 ] @>

[<Fact>]
let ``issueMissing stores the hash of the code it returns`` () =
  let m, code =
    SetupCode.issueMissing [ mkMember 1 "Me" true true ] |> List.exactlyOne

  test
    <@
      match m.Credential with
      | PendingSetup hash -> SetupCode.verify code hash
      | _ -> false
    @>
