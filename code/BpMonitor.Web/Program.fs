module Program

open System
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore
open Serilog
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Web

let private endpoints =
  [ // Anonymous: login/logout
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
    get Routes.trends (AuthHandlers.protect ReadingHandlers.trends)
    get "/trends/{gran}" (AuthHandlers.protect ReadingHandlers.trendsPanel)
    get "/trends/{gran}/{key}" (AuthHandlers.protect ReadingHandlers.trendsPanel)
    get Routes.exportJson (AuthHandlers.protect ReadingHandlers.exportJson)
    get Routes.exportCsv (AuthHandlers.protect ReadingHandlers.exportCsv)
    get Routes.settings (AuthHandlers.protect ReadingHandlers.settings)
    post Routes.settings (AuthHandlers.protect ReadingHandlers.updateSettings)
    post Routes.readings (AuthHandlers.protect ReadingHandlers.createReading)
    get "/readings/{id:int}/edit" (AuthHandlers.protect ReadingHandlers.editReading)
    post "/readings/{id:int}" (AuthHandlers.protect ReadingHandlers.updateReading)
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

    builder.Services
      .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
      .AddCookie(fun o ->
        o.LoginPath <- PathString("/login")
        o.Cookie.HttpOnly <- true
        o.Cookie.SameSite <- SameSiteMode.Strict
        o.Cookie.SecurePolicy <- CookieSecurePolicy.SameAsRequest
        o.SlidingExpiration <- true)
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
        ranges
        TimeProvider.System
        true

    // One structured log line per request (method, path, status, elapsed ms).
    app.UseSerilogRequestLogging() |> ignore

    app.UseStaticFiles().UseRouting().UseAuthentication().UseAuthorization().UseFalco(endpoints)
    |> ignore

    app.Lifetime.ApplicationStarted.Register(fun () ->
      Log.Information("BpMonitor.Web {Version} starting on {Urls}", Version.current, app.Urls))
    |> ignore

    app.Run()
    0
  with ex ->
    Log.Fatal(ex, "BpMonitor.Web terminated unexpectedly")

    1
    |> fun exitCode ->
      Log.CloseAndFlush()
      exitCode
