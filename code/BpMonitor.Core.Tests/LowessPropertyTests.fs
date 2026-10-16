module LowessPropertyTests

open System
open FsCheck
open FsCheck.FSharp
open FsCheck.Xunit
open BpMonitor.Core
open Generators

let private bandwidthGen = Gen.choose (1, 100) |> Gen.map (fun p -> float p / 100.0)

[<Property>]
let ``smooth never returns NaN or Infinity for any finite series and bandwidth`` () =
  let arb = Arb.fromGen (Gen.zip clusteredSeriesGen bandwidthGen)

  Prop.forAll arb (fun ((xs, ys), bandwidth) ->
    let result = Lowess.smooth bandwidth xs ys

    List.length result = List.length ys && result |> List.forall Double.IsFinite)

// With ties at the neighborhood radius, Reference.smooth (unstable sort) picks an arbitrary
// subset, so only inputs without such ties can be compared with it. Distinct x values
// make them rare; `hasTieAtNeighborDistance` skips the rest (e.g. 4 and 6 around 5).
let private distinctSeriesGen: Gen<float list * float list> =
  gen {
    let! n = Gen.choose (3, 40)

    let! xs =
      Gen.choose (0, 100000)
      |> Gen.listOfLength n
      |> Gen.map (List.distinct >> List.map (fun x -> float x / 100.0))

    let! ys = Gen.choose (60, 180) |> Gen.map float |> Gen.listOfLength xs.Length
    return xs, ys
  }

let private hasTieAtNeighborDistance (bandwidth: float) (xs: float list) =
  let n = List.length xs
  let k = max 2 (int (ceil (bandwidth * float n)))

  xs
  |> List.exists (fun xi ->
    let sortedDistances = xs |> List.map (fun x -> abs (x - xi)) |> List.sort
    let neighborhoodRadius = sortedDistances[k - 1]

    let pointsWithinRadius =
      sortedDistances |> List.filter (fun d -> d <= neighborhoodRadius) |> List.length

    pointsWithinRadius > k)

let private nearlyEqual (a: float) (b: float) = abs (a - b) <= 1e-9

// Oracle test comparing the old reference implementation with the improved sliding-window implementation.
[<Property>]
let ``smoothWith 0 matches the reference implementation oracle for inputs without ties`` () =
  let arb = Arb.fromGen (Gen.zip distinctSeriesGen bandwidthGen)

  Prop.forAll arb (fun ((xs, ys), bandwidth) ->
    let improved = Lowess.smoothWith 0 bandwidth xs ys
    let oracle = Lowess.Reference.smoothWith 0 bandwidth xs ys

    hasTieAtNeighborDistance bandwidth xs
    || List.forall2 nearlyEqual improved oracle)

// Ties at the neighborhood radius (e.g. several readings on one day) all count instead of an
// arbitrary subset, so the result can't depend on the order the readings arrive in.
[<Property>]
let ``smoothWith 0 does not depend on the input order, even with many readings on one day`` () =
  let arb = Arb.fromGen (Gen.zip clusteredSeriesGen bandwidthGen)

  Prop.forAll arb (fun ((xs, ys), bandwidth) ->
    let forward = Lowess.smoothWith 0 bandwidth xs ys
    let backward = Lowess.smoothWith 0 bandwidth (List.rev xs) (List.rev ys) |> List.rev
    List.forall2 nearlyEqual forward backward)
