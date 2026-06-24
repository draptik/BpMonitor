module LowessTests

open Xunit
open Swensen.Unquote
open BpMonitor.Core

[<Fact>]
let ``smooth returns the same y values for perfectly linear data`` () =
  let xs = [ 0.0; 1.0; 2.0; 3.0; 4.0 ]
  let ys = [ 0.0; 1.0; 2.0; 3.0; 4.0 ]

  let result = Lowess.smooth 0.5 xs ys

  test <@ List.forall2 (fun a b -> abs (a - b) < 0.001) result ys @>

[<Fact>]
let ``smooth returns the same constant value for constant data`` () =
  let xs = [ 0.0; 1.0; 2.0; 3.0; 4.0 ]
  let ys = [ 7.0; 7.0; 7.0; 7.0; 7.0 ]

  let result = Lowess.smooth 0.5 xs ys

  test <@ List.forall (fun y -> abs (y - 7.0) < 0.001) result @>

[<Fact>]
let ``smooth reduces noise variance around an underlying linear trend`` () =
  let xs = [ 0.0; 1.0; 2.0; 3.0; 4.0; 5.0; 6.0; 7.0; 8.0; 9.0 ]
  let trend = xs |> List.map (fun x -> 2.0 * x + 1.0)
  // Alternating +/- noise around the trend line.
  let noise = [ 3.0; -3.0; 3.0; -3.0; 3.0; -3.0; 3.0; -3.0; 3.0; -3.0 ]
  let ys = List.map2 (+) trend noise

  let result = Lowess.smooth 0.5 xs ys

  let sumSquaredError series =
    List.map2 (fun a b -> (a - b) ** 2.0) series trend |> List.sum

  test <@ sumSquaredError result < sumSquaredError ys @>

[<Fact>]
let ``smooth returns the input unchanged when there are fewer than 3 points`` () =
  let xs = [ 0.0; 1.0 ]
  let ys = [ 5.0; 12.0 ]

  let result = Lowess.smooth 0.5 xs ys

  test <@ result = ys @>

[<Fact>]
let ``smooth keeps the overall direction of a monotone increasing trend`` () =
  let xs = [ 0.0 .. 9.0 ]
  let ys = [ 1.0; 3.0; 2.0; 5.0; 4.0; 7.0; 6.0; 9.0; 8.0; 11.0 ]

  let result = Lowess.smooth 0.5 xs ys

  test <@ List.head result < List.last result @>

[<Fact>]
let ``smooth downweights a single extreme outlier instead of being pulled toward it`` () =
  // A clean linear trend with one wild outlier in the middle. Cleveland's LOWESS
  // is defined by its robustifying iterations (bisquare-reweight on residuals,
  // then refit) — that's precisely what lets it shrug off an outlier reading
  // instead of dragging the local fit toward it like plain weighted regression would.
  let xs = [ 0.0 .. 10.0 ]
  let trend = xs |> List.map (fun x -> 2.0 * x + 1.0)
  let outlierIdx = 5
  let ys = trend |> List.mapi (fun i y -> if i = outlierIdx then y + 100.0 else y)

  let result = Lowess.smooth 1.0 xs ys

  let trueValue = trend[outlierIdx]
  let outlierValue = ys[outlierIdx]
  let smoothedValue = result[outlierIdx]

  test <@ abs (smoothedValue - trueValue) < abs (smoothedValue - outlierValue) @>
  test <@ abs (smoothedValue - trueValue) < 10.0 @>

[<Fact>]
let ``smooth never returns NaN, even when a cluster of duplicate x-values contains an outlier`` () =
  // Duplicate x-values arise in practice from same-day readings (the chart's x-axis is
  // calendar days). A tight cluster of duplicates containing one outlier can, across the
  // robustifying iterations, drive every weight in a point's neighborhood to zero —
  // leaving weightedLinearFitAt dividing 0.0/0.0. Reproduces the /recent chart's NaN-induced
  // trend-line gaps reported against real reading data clustered around same-day duplicates.
  let xs = [ 0.0; 1.0; 2.0; 3.0; 3.0; 3.0; 4.0; 4.0 ]
  let ys = [ 10.0; 12.0; 9.0; 50.0; 8.0; 9.0; 10.0; 11.0 ]

  let result = Lowess.smooth 0.3 xs ys

  test
    <@
      result
      |> List.forall (fun y -> not (System.Double.IsNaN y || System.Double.IsInfinity y))
    @>
