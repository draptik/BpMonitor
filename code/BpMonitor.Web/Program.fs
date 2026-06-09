module Program

open System
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore
open Serilog
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Web

let private endpoints =
  [ get "/" Handlers.landing
    get "/add" Handlers.newReading
    get "/history" Handlers.history
    get "/chart" Handlers.chart
    post "/readings" Handlers.createReading
    get "/readings/{id:int}/edit" Handlers.editReading
    post "/readings/{id:int}" Handlers.updateReading
    get "/members" Handlers.members
    post "/members" Handlers.createMember
    get "/members/{id:int}/edit" Handlers.editMember
    post "/members/{id:int}" Handlers.updateMember
    post "/members/switch" Handlers.switchMember ]

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

    let app = builder.Build()

    // Apply schema migrations once at startup against a transient scope.
    Log.Information("Applying schema migrations…")
    use scope = app.Services.CreateScope()
    SchemaMigrations.apply (scope.ServiceProvider.GetRequiredService<BpMonitorDbContext>())

    // One structured log line per request (method, path, status, elapsed ms).
    app.UseSerilogRequestLogging() |> ignore
    app.UseStaticFiles().UseRouting().UseFalco(endpoints) |> ignore

    Log.Information("BpMonitor.Web {Version} starting on {Urls}", Version.current, app.Urls)
    app.Run()
    0
  with ex ->
    Log.Fatal(ex, "BpMonitor.Web terminated unexpectedly")

    1
    |> fun exitCode ->
      Log.CloseAndFlush()
      exitCode
