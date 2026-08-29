namespace BpMonitor.Web.E2E

open System
open System.Threading.Tasks

/// Polls `isReady` until it succeeds, but fails fast (rather than waiting out
/// the full timeout) if the process has already exited, and always includes
/// captured process output in the failure so a startup crash is diagnosable
/// instead of showing up as a generic "did not become ready" timeout.
module ProcessReadiness =
  let waitUntilReadyAsync
    (isReady: unit -> Task<bool>)
    (hasExited: unit -> bool)
    (capturedOutput: unit -> string)
    (timeout: TimeSpan)
    (port: int)
    : Task =
    task {
      let deadline = DateTime.UtcNow.Add(timeout)
      let mutable ready = false

      while not ready do
        if hasExited () then
          failwith $"process exited before becoming ready on port {port}. Captured output:\n{capturedOutput ()}"

        if DateTime.UtcNow > deadline then
          failwith $"did not become ready on {port} within {timeout}. Captured output:\n{capturedOutput ()}"

        let! r = isReady ()
        ready <- r

        if not ready then
          do! Task.Delay(250)
    }
