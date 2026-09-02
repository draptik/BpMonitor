namespace BpMonitor.Web.E2E

open System
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Net.Sockets
open System.Threading.Tasks
open Microsoft.Playwright
open Xunit

/// A page whose browser context was traced from creation; disposing it flushes the trace zip.
type TracedPage(context: IBrowserContext, page: IPage, tracePath: string) =
  member _.Page = page

  interface IAsyncDisposable with
    member _.DisposeAsync() : ValueTask =
      task {
        do! context.Tracing.StopAsync(TracingStopOptions(Path = tracePath))
        do! context.CloseAsync()
      }
      |> ValueTask

/// Shared test member used to log in against a fresh BpMonitor.Web instance.
/// `SchemaMigrations` auto-seeds a single unclaimed member named "Me" on an
/// empty database, so every E2E test that needs to be logged in claims it
/// with the same password.
module TestAccount =
  let username = "Me"
  let password = "correct-horse-battery-staple"

  /// Claims the default "Me" account (sets its password) and signs in,
  /// leaving `page` on the app's landing page.
  let claimAndLogin (baseUrl: string) (page: IPage) : Task =
    task {
      let! _ = page.GotoAsync($"{baseUrl}/login")
      do! page.FillAsync("#Username", username)
      do! page.ClickAsync("button[type=submit]")

      do! page.FillAsync("#Password", password)
      do! page.FillAsync("#PasswordConfirm", password)
      do! page.ClickAsync("button[type=submit]")

      // Above Playwright's 30s default — not every route this crosses gets warmed up.
      do! page.WaitForURLAsync($"{baseUrl}/", PageWaitForURLOptions(Timeout = 45000f))
    }

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

  let tracesDir () : string =
    let dir = Path.Combine(codeDir (), "TestResults", "traces")
    Directory.CreateDirectory(dir) |> ignore
    dir

/// Boots a real out-of-process BpMonitor.Web instance (real HTTP, fresh temp SQLite
/// file) and drives it with a Playwright Chromium browser; one per `IClassFixture`.
type WebAppFixture() =
  let mutable webProcess: Process = null
  let mutable playwright: IPlaywright = null
  let mutable browser: IBrowser = null
  let mutable dbPath = ""
  let mutable appLogWriter: StreamWriter = null
  let capturedLines = Collections.Generic.Queue<string>()
  let captureLock = obj ()

  let port =
    let listener = new TcpListener(System.Net.IPAddress.Loopback, 0)
    listener.Start()
    let p = (listener.LocalEndpoint :?> System.Net.IPEndPoint).Port
    listener.Stop()
    p

  /// Bounded so a long shared run doesn't grow this without limit; the full app log
  /// still goes to `appLogWriter` on disk.
  let onProcessLine (line: string) =
    lock captureLock (fun () ->
      appLogWriter.WriteLine(line)
      capturedLines.Enqueue(line)

      if capturedLines.Count > 200 then
        capturedLines.Dequeue() |> ignore)

  let capturedOutput () =
    lock captureLock (fun () -> String.concat "\n" capturedLines)

  member val BaseUrl = "" with get, set
  member _.Browser: IBrowser = browser

  /// Overridden by FirefoxWebAppFixture to catch engine-specific regressions.
  abstract member LaunchBrowserAsync: IPlaywright -> Task<IBrowser>
  default _.LaunchBrowserAsync(pw: IPlaywright) = pw.Chromium.LaunchAsync()

  /// Opens an isolated, traced browser context and page; disposing the result
  /// flushes the trace zip regardless of whether the test passed or failed.
  member _.NewTracedPageAsync(?viewport: ViewportSize) : Task<TracedPage> =
    task {
      let opts = BrowserNewContextOptions()
      viewport |> Option.iter (fun v -> opts.ViewportSize <- v)
      let! context = browser.NewContextAsync(opts)
      do! context.Tracing.StartAsync(TracingStartOptions(Screenshots = true, Snapshots = true, Sources = true))
      let! page = context.NewPageAsync()
      let displayName = TestContext.Current.Test.TestDisplayName
      let tracePath = TraceArtifacts.pathFor (RepoLayout.tracesDir ()) displayName
      return TracedPage(context, page, tracePath)
    }

  member private _.WaitUntilReadyAsync() : Task =
    task {
      use client = new HttpClient(Timeout = TimeSpan.FromSeconds 2.0)

      let isReady () =
        task {
          try
            let! resp = client.GetAsync($"http://127.0.0.1:{port}/health")
            return resp.IsSuccessStatusCode
          with _ ->
            return false
        }

      do!
        ProcessReadiness.waitUntilReadyAsync
          isReady
          (fun () -> webProcess.HasExited)
          capturedOutput
          (TimeSpan.FromSeconds 30.0)
          port

      // /health doesn't JIT-warm the request pipeline; pay that cost here, where a slow first response can't fail a test.
      use warmupClient = new HttpClient(Timeout = TimeSpan.FromSeconds 30.0)

      try
        let! _ = warmupClient.GetAsync($"http://127.0.0.1:{port}/login")
        ()
      with _ ->
        ()
    }

  interface IAsyncLifetime with
    member this.InitializeAsync() : ValueTask =
      task {
        this.BaseUrl <- $"http://127.0.0.1:{port}"
        dbPath <- Path.Combine(Path.GetTempPath(), $"bpmonitor-e2e-{Guid.NewGuid():N}.db")
        appLogWriter <- new StreamWriter(Path.Combine(RepoLayout.tracesDir (), $"e2e-app-{port}.log"), AutoFlush = true)

        let webProjectPath =
          Path.Combine(RepoLayout.codeDir (), "BpMonitor.Web", "BpMonitor.Web.fsproj")

        let psi =
          ProcessStartInfo(
            FileName = "dotnet",
            Arguments = $"run --project \"%s{webProjectPath}\" -c Release --no-build -- --urls=%s{this.BaseUrl}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
          )

        psi.EnvironmentVariables["ConnectionStrings__DefaultConnection"] <- $"Data Source={dbPath}"
        psi.EnvironmentVariables["BpMonitor__SeedDemoData"] <- "false"

        let proc = new Process(StartInfo = psi)

        proc.OutputDataReceived.Add(fun e ->
          if e.Data <> null then
            onProcessLine e.Data)

        proc.ErrorDataReceived.Add(fun e ->
          if e.Data <> null then
            onProcessLine e.Data)

        proc.Start() |> ignore
        proc.BeginOutputReadLine()
        proc.BeginErrorReadLine()
        webProcess <- proc

        do! this.WaitUntilReadyAsync()

        let! pw = Playwright.CreateAsync()
        playwright <- pw
        let! b = this.LaunchBrowserAsync pw
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

        if appLogWriter <> null then
          appLogWriter.Dispose()
      }
      |> ValueTask

/// Same as WebAppFixture but on Firefox, for regressions Chromium tolerates silently
/// (e.g. a non-finite MouseEventInit field, which Firefox validates per-spec and throws on).
type FirefoxWebAppFixture() =
  inherit WebAppFixture()
  override _.LaunchBrowserAsync(pw: IPlaywright) = pw.Firefox.LaunchAsync()
