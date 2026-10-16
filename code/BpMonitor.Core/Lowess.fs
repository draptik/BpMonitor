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

    // A whole neighborhood can end up with zero total weight — e.g., a tight cluster of
    // duplicate x-values (same-day readings) where the robustifying iterations bisquare-zero
    // every remaining weight. There's no weighted information left at that point, so fall
    // back to the plain (unweighted) mean of the neighborhood's y-values instead of
    // dividing 0.0/0.0 into NaN.
    if sumW = 0.0 then
      points |> Array.averageBy (fun (_, _, y) -> y)
    else
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

  /// The original implementation (sorts all n points for every point: O(n² log n) time,
  /// n² allocations). Kept as the oracle for LowessPropertyTests and as the baseline in
  /// BpMonitor.Benchmarks; production code uses `smooth`.
  module Reference =
    /// Smooths `ys` against `xs` using Cleveland's LOWESS: a tricube-weighted local linear
    /// regression refined by robustifying iterations that downweight outlier residuals.
    /// `bandwidth` is the fraction of points (0.0–1.0] included in each point's local
    /// neighborhood. Returns one smoothed y per input x, in input order.
    let smoothWith (iterations: int) (bandwidth: float) (xs: float list) (ys: float list) : float list =
      let n = List.length xs

      if n < 3 then
        ys
      else
        let xs = List.toArray xs
        let ys = List.toArray ys

        // Number of neighbors considered for each point's local fit.
        let k = max 2 (int (ceil (bandwidth * float n)))

        // Each point's k nearest neighbors depend only on `xs`, not on the robustness
        // weights — so they're identical across every robustifying iteration. Computed
        // once here rather than re-sorting all n points per point on every iteration.
        let neighbors =
          Array.init n (fun i ->
            let xi = xs[i]

            Array.init n (fun j -> j, xs[j], ys[j], abs (xs[j] - xi))
            |> Array.sortBy (fun (_, _, _, dist) -> dist)
            |> Array.truncate k)

        let fitAt (robustWeights: float array) (i: int) =
          let xi = xs[i]
          let pointNeighbors = neighbors[i]

          let maxDist = pointNeighbors |> Array.map (fun (_, _, _, dist) -> dist) |> Array.max

          let points =
            pointNeighbors
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

        iterate (Array.create n 1.0) iterations |> Array.toList

    let smooth bandwidth xs ys =
      smoothWith robustnessIterations bandwidth xs ys

  /// Smooths `ys` against `xs` using Cleveland's LOWESS: a tricube-weighted local linear
  /// regression refined by robustifying iterations that downweight outlier residuals.
  /// `bandwidth` is the fraction of points (0.0–1.0] included in each point's local
  /// neighborhood. Returns one smoothed y per input x, in input order.
  ///
  /// Works on the points sorted by x, where each point's k nearest neighbors form a
  /// contiguous window found by expanding outwards (O(n·k) time, O(n) extra memory).
  /// The window takes every point up to the k-th distance, ties included, so the result
  /// never depends on input order (e.g. several readings on one day).
  let smoothWith (iterations: int) (bandwidth: float) (xs: float list) (ys: float list) : float list =
    let n = List.length xs

    if n < 3 then
      ys
    else
      let xsIn = List.toArray xs
      let ysIn = List.toArray ys
      let order = Array.init n id |> Array.sortBy (fun i -> xsIn[i])
      let sx = order |> Array.map (fun i -> xsIn[i])
      let sy = order |> Array.map (fun i -> ysIn[i])
      let k = max 2 (int (ceil (bandwidth * float n)))

      // Window [lo, hi] (positions in sorted order) and its largest distance, per point.
      let windows =
        Array.init n (fun p ->
          let xi = sx[p]
          let mutable lo = p
          let mutable hi = p

          for _ in 2 .. min k n do
            if hi = n - 1 || (lo > 0 && xi - sx[lo - 1] <= sx[hi + 1] - xi) then
              lo <- lo - 1
            else
              hi <- hi + 1

          let maxDist = max (xi - sx[lo]) (sx[hi] - xi)

          // Ties at the k-th distance: take them all rather than an arbitrary subset.
          while lo > 0 && xi - sx[lo - 1] <= maxDist do
            lo <- lo - 1

          while hi < n - 1 && sx[hi + 1] - xi <= maxDist do
            hi <- hi + 1

          lo, hi, maxDist)

      // Same weighted linear fit as `weightedLinearFitAt`, summed in place over the window
      // and centered on xi: same-day points then have dx exactly 0, so sxx is exactly 0
      // for them instead of float noise that would turn the slope into garbage.
      // Reused across fits (they run one after another): each neighbor's weight, computed once.
      let weights = Array.zeroCreate n

      let fitAt (robustWeights: float array) (p: int) =
        let lo, hi, maxDist = windows[p]
        let xi = sx[p]
        let mutable sumW = 0.0
        let mutable sumWDx = 0.0
        let mutable sumWY = 0.0

        for q in lo..hi do
          // tricube, multiplied out: `**` (Math.Pow) dominated the run time.
          let w =
            if maxDist = 0.0 then
              robustWeights[q]
            else
              let u = abs (sx[q] - xi) / maxDist
              let t = 1.0 - u * u * u
              t * t * t * robustWeights[q]

          weights[q] <- w
          sumW <- sumW + w
          sumWDx <- sumWDx + w * (sx[q] - xi)
          sumWY <- sumWY + w * sy[q]

        if sumW = 0.0 then
          let mutable sumY = 0.0

          for q in lo..hi do
            sumY <- sumY + sy[q]

          sumY / float (hi - lo + 1)
        else
          let meanDx = sumWDx / sumW
          let meanY = sumWY / sumW
          let mutable sxx = 0.0
          let mutable sxy = 0.0

          for q in lo..hi do
            let w = weights[q]
            let dx = sx[q] - xi - meanDx
            sxx <- sxx + w * dx * dx
            sxy <- sxy + w * dx * (sy[q] - meanY)

          if sxx = 0.0 then meanY else meanY - (sxy / sxx) * meanDx

      let rec iterate (robustWeights: float array) (remaining: int) =
        let fitted = Array.init n (fitAt robustWeights)

        if remaining = 0 then
          fitted
        else
          let residuals = Array.map2 (fun y f -> abs (y - f)) sy fitted
          let medianAbsResidual = Array.sort residuals |> (fun r -> r[r.Length / 2])
          // A near-exact fit leaves a median of float noise (~1e-14) rather than 0.0;
          // treat it as 0, or bisquare would zero almost every weight.
          let scale =
            if medianAbsResidual < 1e-9 then
              0.0
            else
              6.0 * medianAbsResidual

          iterate (residuals |> Array.map (bisquare scale)) (remaining - 1)

      // Back from sorted positions to input order.
      let smoothed = iterate (Array.create n 1.0) iterations
      let result = Array.zeroCreate n

      for p in 0 .. n - 1 do
        result[order[p]] <- smoothed[p]

      List.ofArray result

  /// LOWESS with the standard three robustifying iterations (see `smoothWith`).
  let smooth bandwidth xs ys =
    smoothWith robustnessIterations bandwidth xs ys
