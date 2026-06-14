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
    get Routes.login Handlers.loginPage
    post Routes.login Handlers.loginWithCredentials
    get "/login/{id:int}" Handlers.loginMember
    post "/login/{id:int}" Handlers.loginSubmit
    post Routes.logout Handlers.logout
    // Authenticated: reading CRUD + app pages
    get Routes.home (Handlers.protect Handlers.landing)
    get Routes.add (Handlers.protect Handlers.newReading)
    get Routes.history (Handlers.protect Handlers.history)
    get Routes.chart (Handlers.protect Handlers.chart)
    get Routes.trends (Handlers.protect Handlers.trends)
    get "/trends/{gran}" (Handlers.protect Handlers.trendsPanel)
    get "/trends/{gran}/{key}" (Handlers.protect Handlers.trendsPanel)
    get Routes.exportJson (Handlers.protect Handlers.exportJson)
    get Routes.exportCsv (Handlers.protect Handlers.exportCsv)
    post Routes.readings (Handlers.protect Handlers.createReading)
    get "/readings/{id:int}/edit" (Handlers.protect Handlers.editReading)
    post "/readings/{id:int}" (Handlers.protect Handlers.updateReading)
    // Admin-only: member management
    get Routes.members (Handlers.protectAdmin Handlers.members)
    post Routes.members (Handlers.protectAdmin Handlers.createMember)
    get "/members/{id:int}/edit" (Handlers.protectAdmin Handlers.editMember)
    post "/members/{id:int}" (Handlers.protectAdmin Handlers.updateMember)
    post "/members/{id:int}/reset-password" (Handlers.protectAdmin Handlers.resetPassword) ]

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
    SchemaMigrations.apply (scope.ServiceProvider.GetRequiredService<BpMonitorDbContext>())

    // One structured log line per request (method, path, status, elapsed ms).
    app.UseSerilogRequestLogging() |> ignore

    app.UseStaticFiles().UseRouting().UseAuthentication().UseAuthorization().UseFalco(endpoints)
    |> ignore

    Log.Information("BpMonitor.Web {Version} starting on {Urls}", Version.current, app.Urls)
    app.Run()
    0
  with ex ->
    Log.Fatal(ex, "BpMonitor.Web terminated unexpectedly")

    1
    |> fun exitCode ->
      Log.CloseAndFlush()
      exitCode
