namespace BpMonitor.Web.E2E

open System.IO
open System.Text.RegularExpressions

/// Derives filesystem-safe Playwright trace paths from xunit's test display names.
module TraceArtifacts =

  /// Test display names carry spaces, punctuation and backticks; keep only what is
  /// safe across platforms, so two names differing only by punctuation still diverge.
  let sanitize (displayName: string) : string =
    let safe = Regex.Replace(displayName, "[^A-Za-z0-9]+", "-").Trim('-')
    if safe.Length > 100 then safe.Substring(0, 100) else safe

  let pathFor (resultsDir: string) (displayName: string) : string =
    Path.Combine(resultsDir, $"{sanitize displayName}.zip")
