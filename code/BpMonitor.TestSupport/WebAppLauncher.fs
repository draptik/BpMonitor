namespace BpMonitor.Web.E2E

open System
open System.IO

/// Resolves BpMonitor.Web's built assembly so the E2E fixture can `dotnet exec` it
/// directly instead of paying `dotnet run`'s MSBuild evaluation on every boot.
module WebAppLauncher =

  /// Maps a test project's output dir (bin/Configuration/TFM) onto BpMonitor.Web's built dll.
  let resolveAppDll (codeDir: string) (testOutputDir: string) : string =
    let parts =
      testOutputDir.TrimEnd('/', '\\').Split([| '/'; '\\' |], StringSplitOptions.RemoveEmptyEntries)

    match parts |> Array.tryFindIndexBack (fun p -> p = "bin") with
    | Some i when i + 2 < parts.Length ->
      let configuration = parts[i + 1]
      let tfm = parts[i + 2]
      Path.Combine(codeDir, "BpMonitor.Web", "bin", configuration, tfm, "BpMonitor.Web.dll")
    | _ -> failwith $"Could not resolve build configuration/TFM from test output directory: {testOutputDir}"
