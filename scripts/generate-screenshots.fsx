// Regenerates docs/screenshots/*.png from a real, self-hosted BpMonitor.Web
// instance seeded with the Simpson-family demo dataset.
//
// Run before cutting a release (see the cut-release skill):
//   dotnet fsi scripts/generate-screenshots.fsx
//
// Requires the Playwright Chromium browser (`mise run test:e2e-setup` once
// locally). Uses Ned Flanders — his scripted "elevated readings improving
// after medication" narrative (BpMonitor.Core/DemoData.fs) makes for more
// interesting screenshots than the random-jitter Simpson profiles.
//
// The free-port/HTTP-readiness/process-teardown logic below intentionally
// mirrors code/BpMonitor.Web.E2E.Tests/WebAppFixture.fs — that fixture lives
// in an xunit project built via MSBuild, while this is a standalone `dotnet
// fsi` script with no project reference, so the two can't share a function
// directly. Keep them in sync by hand if the self-hosting approach changes.

#r "nuget: Microsoft.Playwright, 1.61.0"

open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Net.Sockets
open System.Threading.Tasks
open Microsoft.Playwright

let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))
let outDir = Path.Combine(repoRoot, "docs", "screenshots")
let webProjectPath = Path.Combine(repoRoot, "code", "BpMonitor.Web", "BpMonitor.Web.fsproj")

let username = "Ned Flanders"
let password = "screenshot-gen-password"

let pages =
  [ "landing", "/"
    "add", "/add"
    "history", "/history"
    "recent", "/recent"
    "trends", "/trends" ]

let freePort () =
  let listener = new TcpListener(Net.IPAddress.Loopback, 0)
  listener.Start()
  let p = (listener.LocalEndpoint :?> Net.IPEndPoint).Port
  listener.Stop()
  p

let waitUntilReady (baseUrl: string) =
  task {
    use client = new HttpClient(Timeout = TimeSpan.FromSeconds 2.0)
    let deadline = DateTime.UtcNow.AddSeconds 30.0
    let mutable ready = false

    while not ready do
      if DateTime.UtcNow > deadline then
        failwith $"BpMonitor.Web did not become ready on {baseUrl} within 30s"

      try
        let! resp = client.GetAsync($"{baseUrl}/login")
        ready <- resp.IsSuccessStatusCode
      with _ ->
        do! Task.Delay 250
  }

// Waits for network activity to quiet down instead of a fixed sleep — real
// signal for htmx swaps and Plotly's async script fetch settling.
let gotoAndSettle (page: IPage) (url: string) =
  task {
    let! _ = page.GotoAsync url
    do! page.WaitForLoadStateAsync LoadState.NetworkIdle
  }

let setTheme (page: IPage) (theme: string) =
  task {
    do! page.EvaluateAsync($"localStorage.setItem('theme', '{theme}')") :> Task
    let! _ = page.ReloadAsync()
    do! page.WaitForLoadStateAsync LoadState.NetworkIdle
  }

let snap (page: IPage) (name: string) =
  task {
    let! _ = page.ScreenshotAsync(PageScreenshotOptions(Path = Path.Combine(outDir, $"{name}.png")))
    printfn $"saved {name}.png"
  }

// Hovers a value-strip cell partway through the series to show the
// chart<->strip scrubber link (recent-scrubber.js) in action: boxes the strip
// cell and drives the chart's hover spike/tooltip. Scoped to the Systolic row
// (the value strip's first <tr>, per ReadingViews.fs `valueStrip`) so the
// selector doesn't rely on row-count arithmetic to avoid landing on Diastolic.
let captureRecentScrubberShot (page: IPage) (theme: string) =
  task {
    let cells = page.Locator ".value-strip tr:first-child td[data-x]:not(.out-of-range)"
    let! count = cells.CountAsync()

    // Move away first: Playwright tracks cursor position across navigations
    // and skips dispatching a move if the pointer is already at the target.
    do! page.Mouse.MoveAsync(10.0f, 10.0f)
    do! cells.Nth(count / 2).HoverAsync()

    // Wait for the real effect (recent-scrubber.js boxes the matching strip
    // cell on plotly_hover) instead of guessing at an animation delay. Both
    // rows get the "scrubbed" class for the same hovered column, so scope to
    // the first row to keep this a single-element locator.
    do! page.Locator(".value-strip tr:first-child td.scrubbed").WaitForAsync()
    do! snap page $"recent-scrubber-{theme}"
  }

task {
  let port = freePort ()
  let baseUrl = $"http://127.0.0.1:{port}"
  let dbPath = Path.Combine(Path.GetTempPath(), $"bpmonitor-screenshots-{Guid.NewGuid():N}.db")

  // Build once up front so the process we spawn below can start with
  // --no-build — `dotnet run` alone re-evaluates and rebuilds on every
  // invocation, which is wasted work for a script re-run before every release.
  let buildPsi =
    ProcessStartInfo(FileName = "dotnet", Arguments = $"build \"{webProjectPath}\" -c Release", UseShellExecute = false)

  use buildProcess = Process.Start buildPsi
  buildProcess.WaitForExit()

  if buildProcess.ExitCode <> 0 then
    failwith $"dotnet build failed with exit code {buildProcess.ExitCode}"

  let psi =
    ProcessStartInfo(
      FileName = "dotnet",
      Arguments = $"run --no-build --project \"{webProjectPath}\" -c Release -- --urls={baseUrl}",
      UseShellExecute = false
    )

  psi.EnvironmentVariables["ConnectionStrings__DefaultConnection"] <- $"Data Source={dbPath}"
  psi.EnvironmentVariables["BpMonitor__SeedDemoData"] <- "true"

  let webProcess = Process.Start psi
  let mutable playwright: IPlaywright = null
  let mutable browser: IBrowser = null

  try
    do! waitUntilReady baseUrl

    let! pw = Playwright.CreateAsync()
    playwright <- pw
    let! b = playwright.Chromium.LaunchAsync()
    browser <- b
    let! context = browser.NewContextAsync(BrowserNewContextOptions(ViewportSize = ViewportSize(Width = 1440, Height = 900)))
    let! page = context.NewPageAsync()

    // Ned starts unclaimed on a fresh seed — claim the account with a password.
    let! _ = page.GotoAsync $"{baseUrl}/login"
    do! page.FillAsync("#Username", username)
    do! page.ClickAsync "button[type=submit]"
    do! page.FillAsync("#Password", password)
    do! page.FillAsync("#PasswordConfirm", password)
    do! page.ClickAsync "button[type=submit]"

    try
      do! page.WaitForURLAsync $"{baseUrl}/"
    with ex ->
      failwith
        $"Login/claim flow for '{username}' never reached the dashboard — has BpMonitor.Core/DemoData.fs changed (renamed/pre-claimed the member, or changed the claim form)? Underlying error: {ex.Message}"

    Directory.CreateDirectory outDir |> ignore

    for theme in [ "light"; "dark" ] do
      do! setTheme page theme

      for name, path in pages do
        do! gotoAndSettle page $"{baseUrl}{path}"
        do! snap page $"{name}-{theme}"

        if name = "recent" then
          do! captureRecentScrubberShot page theme

    do! browser.CloseAsync()
  finally
    if playwright <> null then
      playwright.Dispose()

    if not webProcess.HasExited then
      webProcess.Kill(entireProcessTree = true)
      webProcess.WaitForExit 5000 |> ignore

    if File.Exists dbPath then
      File.Delete dbPath
}
|> Async.AwaitTask
|> Async.RunSynchronously
