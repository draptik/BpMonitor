module LowessPropertyTests

open FsCheck
open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Core

/// x/y series biased toward the conditions that can make a local LOWESS fit singular:
/// x's drawn from a small discrete set make duplicate x-values (e.g., same-day readings on
/// the real /recent chart) likely, and a few outlier y's are large enough to get
/// bisquare-zeroed during the robustifying iterations — together the combination that
/// drove `weightedLinearFitAt`'s 0.0/0.0 division.
let private clusteredSeriesGen: Gen<float list * float list> =
  gen {
    let! n = Gen.choose (3, 25)
    let! xs = Gen.choose (0, 10) |> Gen.map float |> Gen.listOfLength n

    let! ys =
      Gen.frequency
        [ 8, Gen.choose (60, 180) |> Gen.map float // plausible BP-like values
          1, Gen.choose (-500, -200) |> Gen.map float // low outlier
          1, Gen.choose (500, 1000) |> Gen.map float ] // high outlier
      |> Gen.listOfLength n

    return xs, ys
  }

[<Property>]
let ``smooth never returns NaN or Infinity for any finite series and bandwidth`` () =
  let arb =
    Arb.fromGen (Gen.zip clusteredSeriesGen (Gen.choose (1, 100) |> Gen.map (fun p -> float p / 100.0)))

  Prop.forAll arb (fun ((xs, ys), bandwidth) ->
    let result = Lowess.smooth bandwidth xs ys

    List.length result = List.length ys
    && result
       |> List.forall (fun y -> not (System.Double.IsNaN y || System.Double.IsInfinity y)))
