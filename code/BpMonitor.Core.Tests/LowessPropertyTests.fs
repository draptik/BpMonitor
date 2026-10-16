module LowessPropertyTests

open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Core
open Generators

[<Property>]
let ``smooth never returns NaN or Infinity for any finite series and bandwidth`` () =
  let arb =
    Arb.fromGen (Gen.zip clusteredSeriesGen (Gen.choose (1, 100) |> Gen.map (fun p -> float p / 100.0)))

  Prop.forAll arb (fun ((xs, ys), bandwidth) ->
    let result = Lowess.smooth bandwidth xs ys

    List.length result = List.length ys
    && result
       |> List.forall (fun y -> not (System.Double.IsNaN y || System.Double.IsInfinity y)))

// Mirrors Lowess.smooth's neighbourhood size.
let private neighbourCount (bandwidth: float) (n: int) =
  max 2 (int (ceil (bandwidth * float n)))

// More than k points share one x: they all sit at distance 0 with weight 1, and
// Reference.smooth (unstable sort) averages an arbitrary k of them.
let private hasTieOverflow (bandwidth: float) (xs: float list) =
  let k = neighbourCount bandwidth (List.length xs)
  xs |> List.countBy id |> List.exists (fun (_, count) -> count > k)

[<Property>]
let ``smooth matches the reference implementation outside the tie-overflow case`` () =
  let arb =
    Arb.fromGen (Gen.zip clusteredSeriesGen (Gen.choose (1, 100) |> Gen.map (fun p -> float p / 100.0)))

  Prop.forAll arb (fun ((xs, ys), bandwidth) ->
    hasTieOverflow bandwidth xs
    || List.forall2
      (fun (a: float) b -> abs (a - b) <= 1e-9)
      (Lowess.smooth bandwidth xs ys)
      (Lowess.Reference.smooth bandwidth xs ys))
