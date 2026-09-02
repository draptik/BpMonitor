namespace BpMonitor.Web.E2E

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Net.Sockets
open System.Threading
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

/// Password every claimed E2E member shares; usernames vary per test class.
module TestAccount =
  let password = "correct-horse-battery-staple"

  /// Claims `username` (an unclaimed member) and signs in, leaving `page` on the
  /// app's landing page.
  let claimAndLogin (baseUrl: string) (username: string) (page: IPage) : Task =
    task {
      let! _ = page.GotoAsync($"{baseUrl}/login")
      do! page.FillAsync("#Username", username)
      do! page.ClickAsync("button[type=submit]")

      do! page.FillAsync("#Password", password)
      do! page.FillAsync("#PasswordConfirm", password)
      do! page.ClickAsync("button[type=submit]")

      do! page.WaitForURLAsync($"{baseUrl}/")
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

/// One BpMonitor.Web process and one browser per engine for the whole assembly.
type AppFixture() =
  let mutable webProcess: Process = null
  let mutable playwright: IPlaywright = null
  let mutable chromium: IBrowser = null
  let mutable firefox: IBrowser = null
  let mutable dbPath = ""
  let mutable appLogWriter: StreamWriter = null
  let mutable adminClient: HttpClient = null
  let capturedLines = Queue<string>()
  let captureLock = obj ()
  let memberLock = new SemaphoreSlim(1, 1)
  let mutable nextMemberIndex = 0

  let port =
    let listener = new TcpListener(System.Net.IPAddress.Loopback, 0)
    listener.Start()
    let p = (listener.LocalEndpoint :?> System.Net.IPEndPoint).Port
    listener.Stop()
    p

  /// Bounded so a long-lived shared process doesn't grow this without limit; the
  /// full app log still goes to `appLogWriter` on disk.
  let onProcessLine (line: string) =
    lock captureLock (fun () ->
      appLogWriter.WriteLine(line)
      capturedLines.Enqueue(line)

      if capturedLines.Count > 200 then
        capturedLines.Dequeue() |> ignore)

  let capturedOutput () =
    lock captureLock (fun () -> String.concat "\n" capturedLines)

  member val BaseUrl = "" with get, set
  member _.Chromium: IBrowser = chromium
  member _.Firefox: IBrowser = firefox

  /// Opens an isolated, traced browser context and page on `browser`; disposing the
  /// result flushes the trace zip regardless of whether the test passed or failed.
  member _.NewTracedPageAsync(browser: IBrowser, ?viewport: ViewportSize) : Task<TracedPage> =
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

  /// Creates a fresh, unclaimed, non-admin member via the admin-only POST /members
  /// route. Serialized: concurrent test classes share one SQLite writer.
  member this.CreateMemberAsync() : Task<string> =
    task {
      do! memberLock.WaitAsync()

      try
        nextMemberIndex <- nextMemberIndex + 1
        let name = $"e2e-member-{nextMemberIndex}"
        use body = new FormUrlEncodedContent([ KeyValuePair("Name", name) ])
        let! _ = adminClient.PostAsync($"{this.BaseUrl}/members", body)
        return name
      finally
        memberLock.Release() |> ignore
    }

  member private this.WaitUntilReadyAsync() : Task =
    task {
      use client = new HttpClient(Timeout = TimeSpan.FromSeconds 2.0)

      let isReady () =
        task {
          try
            let! resp = client.GetAsync($"{this.BaseUrl}/health")
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
        let! _ = warmupClient.GetAsync($"{this.BaseUrl}/login")
        ()
      with _ ->
        ()
    }

  /// Claims the auto-seeded admin "Me" member and keeps the authenticated
  /// `HttpClient` around to create per-class members for the rest of the run.
  member private this.ClaimAdminAsync() : Task =
    task {
      let handler = new HttpClientHandler(AllowAutoRedirect = false)
      let client = new HttpClient(handler)

      use step1Body =
        new FormUrlEncodedContent([ KeyValuePair("Username", "Me"); KeyValuePair("Password", "") ])

      let! redirectResp = client.PostAsync($"{this.BaseUrl}/login", step1Body)
      let claimUrl = Uri(Uri(this.BaseUrl), redirectResp.Headers.Location).ToString()

      use step2Body =
        new FormUrlEncodedContent(
          [ KeyValuePair("Password", TestAccount.password)
            KeyValuePair("PasswordConfirm", TestAccount.password) ]
        )

      let! _ = client.PostAsync(claimUrl, step2Body)
      adminClient <- client
    }

  interface IAsyncLifetime with
    member this.InitializeAsync() : ValueTask =
      task {
        this.BaseUrl <- $"http://127.0.0.1:{port}"
        dbPath <- Path.Combine(Path.GetTempPath(), $"bpmonitor-e2e-{Guid.NewGuid():N}.db")
        appLogWriter <- new StreamWriter(Path.Combine(RepoLayout.tracesDir (), $"e2e-app-{port}.log"), AutoFlush = true)

        let codeDir = RepoLayout.codeDir ()
        let webProjectDir = Path.Combine(codeDir, "BpMonitor.Web")
        let appDll = WebAppLauncher.resolveAppDll codeDir AppContext.BaseDirectory

        if not (File.Exists appDll) then
          failwith $"BpMonitor.Web build output not found at {appDll} — build the solution first."

        let psi =
          ProcessStartInfo(
            FileName = "dotnet",
            Arguments = $"exec \"%s{appDll}\" --urls=%s{this.BaseUrl}",
            // dotnet exec doesn't run from the project directory the way `dotnet run` does;
            // wwwroot and appsettings.json are only found there, not next to the built dll.
            WorkingDirectory = webProjectDir,
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
        do! this.ClaimAdminAsync()

        let! pw = Playwright.CreateAsync()
        playwright <- pw
        let! c = pw.Chromium.LaunchAsync()
        chromium <- c
        let! f = pw.Firefox.LaunchAsync()
        firefox <- f
      }
      |> ValueTask

    member _.DisposeAsync() : ValueTask =
      task {
        // Each stage runs even if an earlier one throws: a leaked app process keeps
        // burning CPU on the runner for the rest of the job.
        let errors = ResizeArray<exn>()

        let inline attempt (f: unit -> Task) =
          task {
            try
              do! f ()
            with ex ->
              errors.Add ex
          }

        do!
          attempt (fun () ->
            if chromium <> null then
              chromium.CloseAsync()
            else
              Task.CompletedTask)

        do!
          attempt (fun () ->
            if firefox <> null then
              firefox.CloseAsync()
            else
              Task.CompletedTask)

        do!
          attempt (fun () ->
            if playwright <> null then
              playwright.Dispose()

            Task.CompletedTask)

        do!
          attempt (fun () ->
            if webProcess <> null && not webProcess.HasExited then
              webProcess.Kill(entireProcessTree = true)
              webProcess.WaitForExit(5000) |> ignore

            Task.CompletedTask)

        do!
          attempt (fun () ->
            if File.Exists(dbPath) then
              File.Delete(dbPath)

            if appLogWriter <> null then
              appLogWriter.Dispose()

            if adminClient <> null then
              adminClient.Dispose()

            Task.CompletedTask)

        if errors.Count > 0 then
          raise (AggregateException errors)
      }
      |> ValueTask

/// Per-test-class isolation on the shared app: its own family member (repository
/// queries are member-scoped) and its own browser context per page.
[<AbstractClass>]
type MemberFixture(app: AppFixture) =
  let mutable memberName = ""

  abstract member Browser: IBrowser

  member _.BaseUrl = app.BaseUrl
  member _.MemberName = memberName

  member this.NewTracedPageAsync(?viewport: ViewportSize) : Task<TracedPage> =
    app.NewTracedPageAsync(this.Browser, ?viewport = viewport)

  interface IAsyncLifetime with
    member _.InitializeAsync() : ValueTask =
      task {
        let! name = app.CreateMemberAsync()
        memberName <- name
      }
      |> ValueTask

    member _.DisposeAsync() : ValueTask = ValueTask.CompletedTask

type ChromiumFixture(app: AppFixture) =
  inherit MemberFixture(app)
  override _.Browser = app.Chromium

/// Same as ChromiumFixture but on Firefox, for regressions Chromium tolerates silently
/// (e.g. a non-finite MouseEventInit field, which Firefox validates per-spec and throws on).
type FirefoxFixture(app: AppFixture) =
  inherit MemberFixture(app)
  override _.Browser = app.Firefox
