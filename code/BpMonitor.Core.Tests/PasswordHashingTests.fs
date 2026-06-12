module PasswordHashingTests

open Xunit
open Swensen.Unquote
open FsCheck
open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Core

// ---------------------------------------------------------------------------
// Unit tests
// ---------------------------------------------------------------------------

[<Fact>]
let ``verify accepts the correct password after hash`` () =
  let hash = PasswordHashing.hash "hunter2"
  test <@ PasswordHashing.verify "hunter2" hash @>

[<Fact>]
let ``verify rejects a wrong password`` () =
  let hash = PasswordHashing.hash "hunter2"
  test <@ not (PasswordHashing.verify "wrong" hash) @>

[<Fact>]
let ``verify rejects empty string against a real hash`` () =
  let hash = PasswordHashing.hash "hunter2"
  test <@ not (PasswordHashing.verify "" hash) @>

[<Fact>]
let ``verify returns false on malformed encoded string`` () =
  test <@ not (PasswordHashing.verify "any" "notvalid") @>
  test <@ not (PasswordHashing.verify "any" "") @>
  test <@ not (PasswordHashing.verify "any" "only.twoparts") @>

[<Fact>]
let ``verify returns false when hash section is tampered`` () =
  let encoded = PasswordHashing.hash "secret"
  let parts = encoded.Split('.')
  // Flip the first char of the hash segment to a char that is guaranteed different.
  // Base64 uses A-Z a-z 0-9 + /; rotating the first char by +1 in that alphabet always differs.
  let hashPart = parts[2]
  let base64Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
  let firstChar = hashPart[0]

  let nextChar =
    base64Chars[(base64Chars.IndexOf(firstChar) + 1) % base64Chars.Length]

  let tampered = $"{parts[0]}.{parts[1]}.{nextChar}{hashPart[1..]}"
  test <@ not (PasswordHashing.verify "secret" tampered) @>

[<Fact>]
let ``hashing the same password twice produces different encodings (distinct salts)`` () =
  let h1 = PasswordHashing.hash "password"
  let h2 = PasswordHashing.hash "password"
  test <@ h1 <> h2 @>

// ---------------------------------------------------------------------------
// Property-based tests
// ---------------------------------------------------------------------------

/// Non-empty non-null printable-ASCII strings — covers common password characters.
let private passwordGen: Gen<string> =
  gen {
    let! len = Gen.choose (1, 64)

    let! chars =
      Gen.elements ([ '!' .. '~' ]) // printable ASCII excluding space
      |> Gen.listOfLength len

    return System.String(List.toArray chars)
  }

[<Property>]
let ``verify accepts the correct password for any non-empty password`` () =
  Prop.forAll (Arb.fromGen passwordGen) (fun password ->
    PasswordHashing.verify password (PasswordHashing.hash password))

[<Property>]
let ``verify rejects a different password for any non-empty password pair`` () =
  Prop.forAll (Arb.fromGen (Gen.two passwordGen |> Gen.filter (fun (a, b) -> a <> b))) (fun (p1, p2) ->
    not (PasswordHashing.verify p2 (PasswordHashing.hash p1)))
