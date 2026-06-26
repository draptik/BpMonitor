module LowessPropertyTests

open FsCheck
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
