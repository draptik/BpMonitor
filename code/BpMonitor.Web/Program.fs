module Program

open System
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.DataProtection
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore
open Serilog
open Serilog.Events
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Web

let private endpoints =
  [ // Anonymous: health probe
    get Routes.health HealthHandlers.health
    // Anonymous: login/logout
    get Routes.login AuthHandlers.loginPage
    post Routes.login AuthHandlers.loginWithCredentials
    get "/login/{id:int}" AuthHandlers.loginMember
    post "/login/{id:int}" AuthHandlers.loginSubmit
    post Routes.logout AuthHandlers.logout
    // Authenticated: reading CRUD + app pages
    get Routes.home (AuthHandlers.protect ReadingHandlers.landing)
    get Routes.add (AuthHandlers.protect ReadingHandlers.newReading)
    get Routes.history (AuthHandlers.protect ReadingHandlers.history)
    get Routes.recent (AuthHandlers.protect ReadingHandlers.recent)
    get Routes.recentFull (AuthHandlers.protect ReadingHandlers.recentFull)
    get Routes.trends (AuthHandlers.protect ReadingHandlers.trends)
    get "/trends/{gran}" (AuthHandlers.protect ReadingHandlers.trendsPanel)
    get "/trends/{gran}/{key}" (AuthHandlers.protect ReadingHandlers.trendsPanel)
    get Routes.exportJson (AuthHandlers.protect ReadingHandlers.exportJson)
    get Routes.exportCsv (AuthHandlers.protect ReadingHandlers.exportCsv)
    get Routes.settings (AuthHandlers.protect ReadingHandlers.settings)
    post Routes.settings (AuthHandlers.protect ReadingHandlers.updateSettings)
    post Routes.settingsLanguage (AuthHandlers.protect ReadingHandlers.updateLanguage)
    post Routes.readings (AuthHandlers.protect ReadingHandlers.createReading)
    get "/readings/{id:int}/edit" (AuthHandlers.protect ReadingHandlers.editReading)
    post "/readings/{id:int}" (AuthHandlers.protect ReadingHandlers.updateReading)
    // Authenticated: medication CRUD (self-service, on /settings)
    post Routes.medications (AuthHandlers.protect MedicationHandlers.create)
    get "/medications/{id:int}/edit" (AuthHandlers.protect MedicationHandlers.edit)
    post "/medications/{id:int}" (AuthHandlers.protect MedicationHandlers.update)
    post "/medications/{id:int}/delete" (AuthHandlers.protect MedicationHandlers.delete)
    // Admin-only: member management
    get Routes.members (AuthHandlers.protectAdmin MemberHandlers.members)
    post Routes.members (AuthHandlers.protectAdmin MemberHandlers.createMember)
    get "/members/{id:int}/edit" (AuthHandlers.protectAdmin MemberHandlers.editMember)
    post "/members/{id:int}" (AuthHandlers.protectAdmin MemberHandlers.updateMember)
    post "/members/{id:int}/reset-password" (AuthHandlers.protectAdmin MemberHandlers.resetPassword) ]

[<EntryPoint>]
let main args =
  // Bootstrap logger captures startup failures before the host is built.
  Log.Logger <- LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger()

  try
    let builder = WebApplication.CreateBuilder(args)

    // Replace the default logging pipeline with Serilog, configured from appsettings.
    builder.Host.UseSerilog(fun ctx _services cfg ->
      cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext() |> ignore)
    |> ignore

    let connectionString =
      builder.Configuration.GetConnectionString("DefaultConnection")

    builder.Services.AddDbContext<BpMonitorDbContext>(fun opts -> opts.UseSqlite(connectionString) |> ignore)
    |> ignore

    builder.Services.AddSingleton<TimeProvider>(TimeProvider.System) |> ignore

    builder.Services.AddScoped<IReadingRepository>(fun sp ->
      EfReadingRepository(sp.GetRequiredService<BpMonitorDbContext>(), TimeProvider.System))
    |> ignore

    builder.Services.AddScoped<IFamilyMemberRepository>(fun sp ->
      EfFamilyMemberRepository(sp.GetRequiredService<BpMonitorDbContext>(), TimeProvider.System))
    |> ignore

    builder.Services.AddScoped<IMedicationRepository>(fun sp ->
      EfMedicationRepository(sp.GetRequiredService<BpMonitorDbContext>(), TimeProvider.System))
    |> ignore

    let secureCookies = builder.Configuration.GetValue<bool>("BpMonitor:SecureCookies")
    let rememberMeDays = Config.readRememberMeDays builder.Configuration

    builder.Services
      .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
      .AddCookie(fun o ->
        o.LoginPath <- PathString("/login")
        o.Cookie.HttpOnly <- true
        // Lax (not Strict): a Strict cookie is withheld on top-level navigations that
        // arrive from outside the site — e.g. tapping a BpMonitor link from another
        // Android app — which looks like an unexplained logout even with "remember me"
        // checked. Lax still withholds the cookie on cross-site POSTs, which is what
        // matters here since the app has no antiforgery tokens.
        o.Cookie.SameSite <- SameSiteMode.Lax

        o.Cookie.SecurePolicy <-
          if secureCookies then
            CookieSecurePolicy.Always
          else
            CookieSecurePolicy.SameAsRequest

        o.SlidingExpiration <- true
        // Only takes effect for persistent ("remember me") logins; a non-persistent
        // sign-in still produces a session cookie that dies with the browser process.
        o.ExpireTimeSpan <- TimeSpan.FromDays(float rememberMeDays))
    |> ignore

    // Data Protection keys encrypt/validate the auth cookie. Left unset, they land in
    // the container's ephemeral home directory, so a "remember me" cookie would stop
    // validating on every redeploy. Pointing this at the same volume as the SQLite
    // database keeps it stable across container recreation.
    let dataProtectionKeyPath = builder.Configuration["BpMonitor:DataProtectionKeyPath"]

    let dataProtection =
      builder.Services.AddDataProtection().SetApplicationName("BpMonitor")

    if not (String.IsNullOrWhiteSpace dataProtectionKeyPath) then
      dataProtection.PersistKeysToFileSystem(System.IO.DirectoryInfo(dataProtectionKeyPath))
      |> ignore

    builder.Services.Configure<ForwardedHeadersOptions>(fun (opts: ForwardedHeadersOptions) ->
      opts.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto)
    |> ignore

    builder.Services.AddAuthorization() |> ignore

    let app = builder.Build()

    // Apply schema migrations once at startup against a transient scope.
    Log.Information("Applying schema migrations…")
    use scope = app.Services.CreateScope()
    let sp = scope.ServiceProvider
    SchemaMigrations.apply (sp.GetRequiredService<BpMonitorDbContext>())

    // Optionally seed the Simpson-family demo dataset (off by default).
    let seedDemo = builder.Configuration.GetValue<bool>("BpMonitor:SeedDemoData")

    if seedDemo then
      Log.Information("Seeding Simpson-family demo data…")
      let ranges = Config.readRanges builder.Configuration

      DemoSeeder.seedIfEmpty
        (sp.GetRequiredService<IFamilyMemberRepository>())
        (sp.GetRequiredService<IReadingRepository>())
        (sp.GetRequiredService<IMedicationRepository>())
        ranges
        TimeProvider.System
        true

    // One structured log line per request (method, path, status, elapsed ms).
    // Successful /health polls are dropped to Verbose (below the configured minimum
    // level) so a container HEALTHCHECK doesn't flood stdout; a failing probe still
    // logs at Error.
    app.UseSerilogRequestLogging(fun opts ->
      opts.GetLevel <-
        fun httpCtx _elapsed ex ->
          if ex <> null || httpCtx.Response.StatusCode >= 500 then
            LogEventLevel.Error
          elif httpCtx.Request.Path.StartsWithSegments(PathString Routes.health) then
            LogEventLevel.Verbose
          else
            LogEventLevel.Information)
    |> ignore

    app.UseForwardedHeaders() |> ignore

    app.UseStaticFiles().UseRouting().UseAuthentication().UseAuthorization().UseFalco(endpoints)
    |> ignore

    app.Lifetime.ApplicationStarted.Register(fun () ->
      Log.Information("BpMonitor.Web {Version} starting on {Urls}", Version.current, app.Urls))
    |> ignore

    app.Run()
    0
  with ex ->
    Log.Fatal(ex, "BpMonitor.Web terminated unexpectedly")

    Log.CloseAndFlush()
    1
