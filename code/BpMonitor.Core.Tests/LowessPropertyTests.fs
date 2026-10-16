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

// With ties at the k-th distance, Reference.smooth (unstable sort) picks an arbitrary
// subset, so only inputs without such ties can be compared with it. Distinct x values
// make them rare; `hasTieAtNeighbourDistance` skips the rest (e.g. 4 and 6 around 5).
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

let private hasTieAtNeighbourDistance (bandwidth: float) (xs: float list) =
  let n = List.length xs
  let k = max 2 (int (ceil (bandwidth * float n)))

  xs
  |> List.exists (fun xi ->
    let dists = xs |> List.map (fun x -> abs (x - xi)) |> List.sort
    let dk = dists[k - 1]
    (dists |> List.filter (fun d -> d <= dk) |> List.length) > k)

// At least half the points fitted exactly: the robustness scale is then 0 in one
// implementation and float noise in the other, which flips the bisquare weights.
let private nearExactFit (ys: float list) (fit: float list) =
  (List.map2 (fun (y: float) f -> abs (y - f) < 1e-6) ys fit
   |> List.filter id
   |> List.length)
  * 2
  >= List.length ys

// Compared without robustifying iterations: those are discontinuous (a residual just past
// the bisquare cutoff drops a point entirely), so float noise from a different summation
// order can flip them. The neighbour selection and weighted fit — what changed — can't.
[<Property>]
let ``smoothWith 0 matches the reference implementation for inputs without ties`` () =
  let arb =
    Arb.fromGen (Gen.zip distinctSeriesGen (Gen.choose (1, 100) |> Gen.map (fun p -> float p / 100.0)))

  Prop.forAll arb (fun ((xs, ys), bandwidth) ->
    hasTieAtNeighbourDistance bandwidth xs
    || List.forall2
      (fun (a: float) b -> abs (a - b) <= 1e-9)
      (Lowess.smoothWith 0 bandwidth xs ys)
      (Lowess.Reference.smoothWith 0 bandwidth xs ys))

// Ties at the k-th distance (e.g. several readings on one day) all count instead of an
// arbitrary subset, so the result can't depend on the order the readings arrive in.
[<Property>]
let ``smoothWith 0 does not depend on the input order, even with many readings on one day`` () =
  let arb =
    Arb.fromGen (Gen.zip clusteredSeriesGen (Gen.choose (1, 100) |> Gen.map (fun p -> float p / 100.0)))

  Prop.forAll arb (fun ((xs, ys), bandwidth) ->
    let forward = Lowess.smoothWith 0 bandwidth xs ys
    let backward = Lowess.smoothWith 0 bandwidth (List.rev xs) (List.rev ys) |> List.rev
    List.forall2 (fun (a: float) b -> abs (a - b) <= 1e-9) forward backward)
