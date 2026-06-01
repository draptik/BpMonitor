module Program

open System
open Falco
open Falco.Routing
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.EntityFrameworkCore
open BpMonitor.Core
open BpMonitor.Data
open BpMonitor.Web

let private endpoints =
  [ get "/" Handlers.dashboard
    get "/chart" Handlers.chart
    get "/readings/new" Handlers.newReading
    post "/readings" Handlers.createReading
    get "/readings/{id:int}/edit" Handlers.editReading
    post "/readings/{id:int}" Handlers.updateReading ]

[<EntryPoint>]
let main args =
  let builder = WebApplication.CreateBuilder(args)

  let connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")

  builder.Services.AddDbContext<BpMonitorDbContext>(fun opts -> opts.UseSqlite(connectionString) |> ignore)
  |> ignore

  builder.Services.AddScoped<IReadingRepository>(fun sp ->
    EfReadingRepository(sp.GetRequiredService<BpMonitorDbContext>(), TimeProvider.System))
  |> ignore

  let app = builder.Build()

  // Apply schema migrations once at startup against a transient scope.
  use scope = app.Services.CreateScope()
  SchemaMigrations.apply (scope.ServiceProvider.GetRequiredService<BpMonitorDbContext>())

  app.UseStaticFiles().UseRouting().UseFalco(endpoints) |> ignore
  app.Run()
  0
