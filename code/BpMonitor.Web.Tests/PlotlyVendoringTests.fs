module PlotlyVendoringTests

open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Web

let private defaultMember = HandlerTestHelpers.sampleMember

[<Fact>]
let ``layout loads plotly.js from a local path, not the CDN`` () =
  let html = renderHtml (ReadingViews.landing defaultMember)

  test <@ not (html.Contains "cdn.plot.ly") @>
  test <@ html.Contains "src=\"/plotly-2.27.1.min.js\"" @>

[<Fact>]
let ``vendored plotly.js matches the resource embedded in the restored Plotly.NET package`` () =
  // Plotly.NET is restored next to this test dll (transitively, via BpMonitor.Charts)
  // but may not be in the AppDomain yet if nothing has touched it — load explicitly.
  let plotlyAssembly =
    Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Plotly.NET.dll"))

  let resourceName =
    plotlyAssembly.GetManifestResourceNames()
    |> Array.tryFind (fun n -> Regex.IsMatch(n, @"^Plotly\.NET\.plotly-[\d.]+\.min\.js$"))
    |> Option.defaultWith (fun () -> failwith "No plotly-*.min.js resource found in Plotly.NET assembly")

  use resourceStream = plotlyAssembly.GetManifestResourceStream resourceName
  use resourceMemory = new MemoryStream()
  resourceStream.CopyTo resourceMemory
  let resourceBytes = resourceMemory.ToArray()

  let version = Regex.Match(resourceName, @"plotly-([\d.]+)\.min\.js").Groups[1].Value

  let vendoredPath =
    Path.Combine(__SOURCE_DIRECTORY__, "..", "BpMonitor.Web", "wwwroot", $"plotly-{version}.min.js")

  test <@ File.Exists vendoredPath @>
  let vendoredBytes = File.ReadAllBytes vendoredPath

  test <@ vendoredBytes = resourceBytes @>
