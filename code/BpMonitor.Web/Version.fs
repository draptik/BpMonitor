namespace BpMonitor.Web

/// Version helpers for the web app. The stamped version is baked in at publish
/// time via -p:Version=<tag>; local/dev builds fall back to "dev".
module Version =
  open System
  open System.Reflection

  /// Normalise a raw InformationalVersion string to a display version.
  /// The .NET default "1.0.0" and empty/None mean the build was not stamped
  /// → returns "dev". Build metadata after '+' is preserved as-is.
  let parse (raw: string option) : string =
    match raw with
    | None -> "dev"
    | Some s ->
      if String.IsNullOrWhiteSpace s || s = "1.0.0" then
        "dev"
      else
        s

  /// The running app's display version, derived from AssemblyInformationalVersion.
  let current: string =
    Assembly.GetEntryAssembly()
    |> Option.ofObj
    |> Option.bind (fun a -> a.GetCustomAttribute<AssemblyInformationalVersionAttribute>() |> Option.ofObj)
    |> Option.map (fun a -> a.InformationalVersion)
    |> parse

  /// GitHub release page URL for a stamped version; None for the "dev" fallback.
  let releaseUrl (v: string) : string option =
    if v = "dev" then
      None
    else
      Some $"https://github.com/draptik/BpMonitor/releases/tag/v{v}"
