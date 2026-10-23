module PasswordHashingTests

open System
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
let ``verify returns false when the iteration count is non-numeric`` () =
  test <@ not (PasswordHashing.verify "any" "abc.def.ghi") @>

[<Fact>]
let ``verify returns false when the salt segment is not valid base64`` () =
  test <@ not (PasswordHashing.verify "any" "310000.not-valid-base64!.AAAA") @>

[<Fact>]
let ``verify returns false when the hash segment is not valid base64`` () =
  test <@ not (PasswordHashing.verify "any" "310000.AAAA.not-valid-base64!") @>

[<Fact>]
let ``verify returns false when the hash segment is empty`` () =
  test <@ not (PasswordHashing.verify "any" "310000.AAAA.") @>

[<Fact>]
let ``verify returns false when the hash segment is shorter than 32 bytes`` () =
  let parts = (PasswordHashing.hash "secret").Split('.')
  let firstByte = Convert.FromBase64String(parts[2])[..0]
  let truncated = $"{parts[0]}.{parts[1]}.{Convert.ToBase64String firstByte}"
  test <@ not (PasswordHashing.verify "secret" truncated) @>

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

/// Non-empty strings of printable ASCII plus umlauts, accents and an emoji (a surrogate pair).
let private passwordGen: Gen<string> =
  gen {
    let! len = Gen.choose (1, 64)

    let! parts =
      Gen.frequency
        [
          4, Gen.elements [ '!' .. '~' ] |> Gen.map string // printable ASCII excluding space
          1, Gen.elements [ "ä"; "ö"; "ü"; "ß"; "é"; "€"; "😀" ]
        ]
      |> Gen.listOfLength len

    return String.concat "" parts
  }

[<Property>]
let ``verify accepts the correct password for any non-empty password`` () =
  Prop.forAll (Arb.fromGen passwordGen) (fun password ->
    PasswordHashing.verify password (PasswordHashing.hash password))

[<Property>]
let ``verify rejects a different password for any non-empty password pair`` () =
  Prop.forAll (Arb.fromGen (Gen.two passwordGen |> Gen.filter (fun (a, b) -> a <> b))) (fun (p1, p2) ->
    not (PasswordHashing.verify p2 (PasswordHashing.hash p1)))

/// Two passwords that differ only in one non-ASCII character, e.g. "pässwort" vs. "pösswort".
let private nonAsciiNeighborsGen: Gen<string * string> =
  gen {
    let! prefix = passwordGen
    let! suffix = passwordGen

    let! c1, c2 =
      Gen.elements [ "ä"; "ö"; "ü"; "ß"; "é"; "€"; "😀" ]
      |> Gen.two
      |> Gen.filter (fun (a, b) -> a <> b)

    return prefix + c1 + suffix, prefix + c2 + suffix
  }

// Each case runs PBKDF2 twice, so keep the run short.
[<Property(MaxTest = 20)>]
let ``verify rejects a password that differs only in one non-ASCII character`` () =
  Prop.forAll (Arb.fromGen nonAsciiNeighborsGen) (fun (p1, p2) ->
    not (PasswordHashing.verify p2 (PasswordHashing.hash p1)))
