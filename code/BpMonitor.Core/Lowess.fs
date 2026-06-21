namespace BpMonitor.Core

/// Locally weighted scatter plot smoothing (LOWESS, Cleveland 1979): a tricube-weighted
/// local linear regression with robustifying iterations, used to draw a trend curve
/// through noisy per-reading data (e.g., the /recent chart) without depending on an
/// external statistics library. The robustifying iterations are LOWESS's defining trait over
/// plain local regression — they downweight outlier readings instead of being pulled
/// toward them, which is precisely why Wegier et al. 2021 chose it for BP displays.
module Lowess =

  // A standard number of robustifying (reweight-and-refit) passes after the initial fit,
  // matching the conventional default (e.g., R's `lowess`, statsmodels' `lowess`).
  let private robustnessIterations = 3

  let private tricube (maxDist: float) (dist: float) =
    if maxDist = 0.0 then
      1.0
    else
      let u = dist / maxDist
      (1.0 - u ** 3.0) ** 3.0

  // Bisquare weight of a residual scaled by 6x the median absolute residual — Cleveland's
  // robustness weighting. Residuals beyond 6 MAD are fully excluded from the next fit.
  let private bisquare (scale: float) (residual: float) =
    if scale = 0.0 then
      1.0
    else
      let u = residual / scale
      if abs u >= 1.0 then 0.0 else (1.0 - u * u) ** 2.0

  // One weighted linear regression y = a + b*x over `points` (weight, x, y), evaluated at `xi`.
  let private weightedLinearFitAt (xi: float) (points: (float * float * float) array) =
    let sumW = points |> Array.sumBy (fun (w, _, _) -> w)
    let meanX = (points |> Array.sumBy (fun (w, x, _) -> w * x)) / sumW
    let meanY = (points |> Array.sumBy (fun (w, _, y) -> w * y)) / sumW
    let sxx = points |> Array.sumBy (fun (w, x, _) -> w * (x - meanX) * (x - meanX))
    let sxy = points |> Array.sumBy (fun (w, x, y) -> w * (x - meanX) * (y - meanY))

    if sxx = 0.0 then
      meanY
    else
      let b = sxy / sxx
      let a = meanY - b * meanX
      a + b * xi

  /// Smooths `ys` against `xs` using Cleveland's LOWESS: a tricube-weighted local linear
  /// regression refined by robustifying iterations that downweight outlier residuals.
  /// `bandwidth` is the fraction of points (0.0–1.0] included in each point's local
  /// neighborhood. Returns one smoothed y per input x, in input order.
  let smooth (bandwidth: float) (xs: float list) (ys: float list) : float list =
    let n = List.length xs

    if n < 3 then
      ys
    else
      let xs = List.toArray xs
      let ys = List.toArray ys

      // Number of neighbours considered for each point's local fit.
      let k = max 2 (int (ceil (bandwidth * float n)))

      // Each point's k nearest neighbours depend only on `xs`, not on the robustness
      // weights — so they're identical across every robustifying iteration. Computed
      // once here rather than re-sorting all n points per point on every iteration.
      let neighbours =
        Array.init n (fun i ->
          let xi = xs[i]

          Array.init n (fun j -> j, xs[j], ys[j], abs (xs[j] - xi))
          |> Array.sortBy (fun (_, _, _, dist) -> dist)
          |> Array.truncate k)

      let fitAt (robustWeights: float array) (i: int) =
        let xi = xs[i]
        let pointNeighbours = neighbours[i]

        let maxDist =
          pointNeighbours |> Array.map (fun (_, _, _, dist) -> dist) |> Array.max

        let points =
          pointNeighbours
          |> Array.map (fun (j, x, y, dist) -> tricube maxDist dist * robustWeights[j], x, y)

        weightedLinearFitAt xi points

      let rec iterate (robustWeights: float array) (remaining: int) =
        let fitted = Array.init n (fitAt robustWeights)

        if remaining = 0 then
          fitted
        else
          let residuals = Array.map2 (fun y f -> abs (y - f)) ys fitted
          let medianAbsResidual = residuals |> Array.sort |> (fun r -> r[r.Length / 2])
          let scale = 6.0 * medianAbsResidual
          let nextWeights = residuals |> Array.map (bisquare scale)
          iterate nextWeights (remaining - 1)

      iterate (Array.create n 1.0) robustnessIterations |> Array.toList
