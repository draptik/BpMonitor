namespace BpMonitor.Web.E2E

open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Net.Sockets
open System.Threading.Tasks
open Microsoft.Playwright
open Xunit

/// Locates the repository's `code/` directory (the one containing BpMonitor.slnx)
/// by walking up from the test assembly's own output directory.
module private RepoLayout =
  let rec private findUpwards (marker: string) (dir: DirectoryInfo) : DirectoryInfo =
    if File.Exists(Path.Combine(dir.FullName, marker)) then
      dir
    elif dir.Parent = null then
      failwith $"Could not locate '{marker}' above {AppContext.BaseDirectory}"
    else
      findUpwards marker dir.Parent

  let codeDir () : string =
    (findUpwards "BpMonitor.slnx" (DirectoryInfo(AppContext.BaseDirectory))).FullName

/// Boots a real BpMonitor.Web instance as a child process (out-of-process, real
/// HTTP, real SQLite file) and drives it with a Playwright Chromium browser.
/// Each test class gets its own instance + fresh temp database via xunit's
/// `IClassFixture`.
type WebAppFixture() =
  let mutable webProcess: Process = null
  let mutable playwright: IPlaywright = null
  let mutable browser: IBrowser = null
  let mutable dbPath = ""

  let port =
    let listener = new TcpListener(System.Net.IPAddress.Loopback, 0)
    listener.Start()
    let p = (listener.LocalEndpoint :?> System.Net.IPEndPoint).Port
    listener.Stop()
    p

  member val BaseUrl = "" with get, set
  member _.Browser: IBrowser = browser

  member private _.WaitUntilReadyAsync() : Task =
    task {
      use client = new HttpClient(Timeout = TimeSpan.FromSeconds 2.0)
      let deadline = DateTime.UtcNow.AddSeconds(30.0)
      let mutable ready = false

      while not ready do
        if DateTime.UtcNow > deadline then
          failwith $"BpMonitor.Web did not become ready on {port} within 30s"

        try
          let! resp = client.GetAsync($"http://127.0.0.1:{port}/login")
          ready <- resp.IsSuccessStatusCode
        with _ ->
          do! Task.Delay(250)
    }

  interface IAsyncLifetime with
    member this.InitializeAsync() : ValueTask =
      task {
        this.BaseUrl <- $"http://127.0.0.1:{port}"
        dbPath <- Path.Combine(Path.GetTempPath(), $"bpmonitor-e2e-{Guid.NewGuid():N}.db")

        let webProjectPath =
          Path.Combine(RepoLayout.codeDir (), "BpMonitor.Web", "BpMonitor.Web.fsproj")

        let psi =
          ProcessStartInfo(
            FileName = "dotnet",
            Arguments = $"run --project \"%s{webProjectPath}\" -c Release --no-build -- --urls=%s{this.BaseUrl}",
            UseShellExecute = false
          )

        psi.EnvironmentVariables["ConnectionStrings__DefaultConnection"] <- $"Data Source={dbPath}"
        psi.EnvironmentVariables["BpMonitor__SeedDemoData"] <- "false"

        webProcess <- Process.Start(psi)
        do! this.WaitUntilReadyAsync()

        let! pw = Playwright.CreateAsync()
        playwright <- pw
        let! b = playwright.Chromium.LaunchAsync()
        browser <- b
      }
      |> ValueTask

    member _.DisposeAsync() : ValueTask =
      task {
        if browser <> null then
          do! browser.CloseAsync()

        if playwright <> null then
          playwright.Dispose()

        if webProcess <> null && not webProcess.HasExited then
          webProcess.Kill(entireProcessTree = true)
          webProcess.WaitForExit(5000) |> ignore

        if File.Exists(dbPath) then
          File.Delete(dbPath)
      }
      |> ValueTask
