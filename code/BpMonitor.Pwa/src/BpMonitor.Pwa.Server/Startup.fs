module BpMonitor.Pwa.Server.Program

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Bolero
open Bolero.Remoting.Server
open Bolero.Server
open BpMonitor.Pwa
open Bolero.Templating.Server

[<EntryPoint>]
let main args =
  let builder = WebApplication.CreateBuilder(args)

  builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents()
  |> ignore

  builder.Services.AddServerSideBlazor() |> ignore

  builder.Services.AddAuthorization().AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie()
  |> ignore

  builder.Services.AddBoleroComponents() |> ignore
#if DEBUG
  builder.Services.AddHotReload(templateDir = __SOURCE_DIRECTORY__ + "/../BpMonitor.Pwa.Client")
  |> ignore
#endif

  let app = builder.Build()

  if app.Environment.IsDevelopment() then
    app.UseWebAssemblyDebugging()

  app.UseBlazorFrameworkFiles().UseAuthentication().UseStaticFiles().UseRouting().UseAuthorization().UseAntiforgery()
  |> ignore

#if DEBUG
  app.UseHotReload()
#endif
  app.MapBoleroRemoting() |> ignore

  app
    .MapRazorComponents<Index.Page>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof<Client.Main.MyApp>.Assembly)
  |> ignore

  app.Run()
  0
