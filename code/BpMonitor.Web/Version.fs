namespace BpMonitor.Web

/// Version helpers for the web app. The stamped version is baked in at publish
/// time via -p:Version=<tag>; local/dev builds fall back to "dev".
module Version =
  open System
  open System.Reflection

  /// Normalise a raw InformationalVersion string to a display version.
  /// The .NET SDK appends +<sha> to the default "1.0.0" in git repos, so strip
  /// the suffix before checking the sentinel — but keep it for real versions.
  let parse (raw: string option) : string =
    match raw with
    | None -> "dev"
    | Some s ->
      let base' =
        match s.IndexOf('+') with
        | -1 -> s
        | i -> s.Substring(0, i)

      if String.IsNullOrWhiteSpace base' || (base' = "1.0.0" && s.Contains('+')) then
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
