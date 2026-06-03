module VersionPropertyTests

open System
open FsCheck
open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Web

let private stampedVersionGen =
  gen {
    let! major = Gen.choose (0, 99)
    let! minor = Gen.choose (0, 99)
    let! patch = Gen.choose (0, 99)
    let base' = $"{major}.{minor}.{patch}"
    let! includeSha = Gen.elements [ true; false ]

    if includeSha then
      let! sha = Gen.elements [ "abc123"; "deadbeef"; "0000000" ]
      return $"{base'}+{sha}"
    else
      return base'
  }
  |> Gen.where (fun s -> s <> "1.0.0")

let private whitespaceGen =
  Gen.nonEmptyListOf (Gen.elements [ ' '; '\t'; '\r'; '\n' ])
  |> Gen.map (fun chars -> String(Array.ofList chars))

[<Property>]
let ``parse always returns a non-empty string`` (raw: string option) =
  not (String.IsNullOrEmpty(Version.parse raw))

[<Property>]
let ``parse returns dev for any whitespace-only string`` () =
  Prop.forAll (Arb.fromGen whitespaceGen) (fun s -> Version.parse (Some s) = "dev")

[<Property>]
let ``parse is identity for any stamped version string`` () =
  Prop.forAll (Arb.fromGen stampedVersionGen) (fun s -> Version.parse (Some s) = s)
