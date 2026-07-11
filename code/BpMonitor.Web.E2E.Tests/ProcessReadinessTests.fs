module BpMonitor.Web.E2E.ProcessReadinessTests

open System
open System.Diagnostics
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Xunit

type ProcessReadinessTests() =

  [<Fact>]
  member _.``throws immediately with captured output when the process has already exited``() : Task =
    task {
      let stopwatch = Stopwatch.StartNew()

      let! ex =
        Assert.ThrowsAsync<Exception>(fun () ->
          ProcessReadiness.waitUntilReadyAsync
            (fun () -> Task.FromResult false)
            (fun () -> true)
            (fun () -> "boom: address already in use")
            (TimeSpan.FromSeconds 30.0)
            1234)

      stopwatch.Stop()

      Assert.Contains("boom: address already in use", ex.Message)
      Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds 5.0)
    }

  [<Fact>]
  member _.``throws with captured output when the timeout elapses without becoming ready``() : Task =
    task {
      let! ex =
        Assert.ThrowsAsync<Exception>(fun () ->
          ProcessReadiness.waitUntilReadyAsync
            (fun () -> Task.FromResult false)
            (fun () -> false)
            (fun () -> "still starting up...")
            (TimeSpan.FromMilliseconds 100.0)
            5678)

      Assert.Contains("still starting up...", ex.Message)
    }

  [<Fact>]
  member _.``completes without throwing once isReady reports true``() : Task =
    ProcessReadiness.waitUntilReadyAsync
      (fun () -> Task.FromResult true)
      (fun () -> false)
      (fun () -> "")
      (TimeSpan.FromSeconds 30.0)
      9999
