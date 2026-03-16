module BpMonitor.Pwa.Server.Index

open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Web
open Bolero
open Bolero.Html
open Bolero.Server.Html
open BpMonitor.Pwa

let page =
  doctypeHtml {
    head {
      meta { attr.charset "UTF-8" }

      meta {
        attr.name "viewport"
        attr.content "width=device-width, initial-scale=1.0"
      }

      title { "Bolero Application" }
      ``base`` { attr.href "/" }

      link {
        attr.rel "stylesheet"
        attr.href "BpMonitor.Pwa.Client.styles.css"
      }
    }

    body {
      div {
        attr.id "main"
        comp<Client.Main.MyApp> { attr.renderMode RenderMode.InteractiveWebAssembly }
      }

      script { attr.src "_framework/blazor.webassembly.js" }
    }
  }

[<Route "/{*path}">]
type Page() =
  inherit Bolero.Component()
  override _.Render() = page
