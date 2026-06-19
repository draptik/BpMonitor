// Vendors the plotly.js build embedded inside the Plotly.NET package into
// BpMonitor.Web/wwwroot, so the web app serves it locally instead of via CDN.
//
// Run after every Plotly.NET version bump:
//   dotnet fsi scripts/extract-plotly-js.fsx
//
// A drift-check test (BpMonitor.Web.Tests) fails CI if the vendored file
// stops matching the resource embedded in the currently restored package.

open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions

let repoRoot = Path.Combine(__SOURCE_DIRECTORY__, "..")

let plotlyVersion =
    let propsPath = Path.Combine(repoRoot, "code", "Directory.Packages.props")
    let content = File.ReadAllText propsPath
    let m = Regex.Match(content, """<PackageVersion Include="Plotly.NET" Version="([^"]+)" />""")
    if not m.Success then failwith "Could not find Plotly.NET version in Directory.Packages.props"
    m.Groups[1].Value

let nugetPackagesRoot =
    match Environment.GetEnvironmentVariable "NUGET_PACKAGES" with
    | null | "" -> Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.UserProfile, ".nuget", "packages")
    | dir -> dir

let assemblyPath =
    Path.Combine(nugetPackagesRoot, "plotly.net", plotlyVersion, "lib", "netstandard2.0", "Plotly.NET.dll")

if not (File.Exists assemblyPath) then
    failwithf "Plotly.NET.dll not found at %s — run 'dotnet restore' first" assemblyPath

let assembly = Assembly.LoadFrom assemblyPath

// The embedded resource name carries the plotly.js version, which is independent
// of (and usually behind) the Plotly.NET package version — find it by pattern.
let resourceName =
    assembly.GetManifestResourceNames()
    |> Array.tryFind (fun n -> Regex.IsMatch(n, @"^Plotly\.NET\.plotly-[\d.]+\.min\.js$"))
    |> Option.defaultWith (fun () -> failwithf "No plotly-*.min.js resource found in %s" assemblyPath)

let licenseResourceName = $"{resourceName}.LICENSE.txt"
let plotlyJsVersion = Regex.Match(resourceName, @"plotly-([\d.]+)\.min\.js").Groups[1].Value

let extractResource (resourceName: string) (destPath: string) =
    use stream = assembly.GetManifestResourceStream resourceName
    if isNull stream then failwithf "Embedded resource '%s' not found in %s" resourceName assemblyPath
    use fileStream = File.Create destPath
    stream.CopyTo fileStream

let wwwroot = Path.Combine(repoRoot, "code", "BpMonitor.Web", "wwwroot")
let jsDest = Path.Combine(wwwroot, $"plotly-{plotlyJsVersion}.min.js")
let licenseDest = Path.Combine(wwwroot, $"plotly-{plotlyJsVersion}.min.js.LICENSE.txt")

extractResource resourceName jsDest
extractResource licenseResourceName licenseDest

printfn "Vendored %s" jsDest
printfn "Vendored %s" licenseDest
