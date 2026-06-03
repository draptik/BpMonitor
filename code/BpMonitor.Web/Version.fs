namespace BpMonitor.Web

/// Version helpers for the web app. The stamped version is baked in at publish
/// time via -p:Version=<tag>; local/dev builds fall back to "dev".
module Version =
  open System
  open System.Reflection

  /// Normalise a raw InformationalVersion string to a display version.
  /// Strips build metadata after '+'; the .NET default "1.0.0" and
  /// empty/None values mean the build was not stamped → returns "dev".
  let parse (raw: string option) : string =
    match raw with
    | None -> "dev"
    | Some s ->
      let v =
        match s.IndexOf('+') with
        | -1 -> s
        | i -> s.Substring(0, i)

      if String.IsNullOrWhiteSpace v || v = "1.0.0" then
        "dev"
      else
        v

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
