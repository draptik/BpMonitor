module BpMonitor.Benchmarks.Program

open BenchmarkDotNet.Running

[<EntryPoint>]
let main args =
  BenchmarkSwitcher.FromAssembly(typeof<LowessBenchmarks>.Assembly).Run(args)
  |> ignore

  0
