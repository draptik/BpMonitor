namespace BpMonitor.Benchmarks

open System
open BenchmarkDotNet.Attributes
open BpMonitor.Core

/// Compares the original LOWESS (Lowess.Reference) with the sliding-window one, on
/// one and five years of readings at three a day — /recent's default and "Load full history".
[<MemoryDiagnoser>]
type LowessBenchmarks() =
  // Same as Charts.fs's lowessBandwidth.
  let bandwidth = 0.12
  let mutable xs: float list = []
  let mutable ys: float list = []

  [<Params(1095, 5475)>]
  member val Readings = 0 with get, set

  [<GlobalSetup>]
  member this.Setup() =
    let rnd = Random(42)
    // Day resolution like the chart's x-axis: three readings share each day's x.
    xs <- List.init this.Readings (fun i -> float (i / 3))
    ys <- xs |> List.map (fun _ -> 120.0 + rnd.NextDouble() * 30.0)

  [<Benchmark(Baseline = true)>]
  member _.Reference() = Lowess.Reference.smooth bandwidth xs ys

  [<Benchmark>]
  member _.SlidingWindow() = Lowess.smooth bandwidth xs ys
