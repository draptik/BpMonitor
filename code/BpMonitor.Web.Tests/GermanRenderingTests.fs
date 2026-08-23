module GermanRenderingTests

open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Web
open ViewTestHelpers

let private de = LocalizedStrings.de

[<Fact>]
let ``layout renders the sidebar and page title in German`` () =
  let html =
    renderHtml (ReadingViews.landing de { defaultMember with Language = German })

  test <@ html.Contains "Verlauf" @>
  test <@ html.Contains "Einstellungen" @>
  test <@ html.Contains "Mitglieder" @>
  test <@ html.Contains "<html lang=\"de\">" @>

[<Fact>]
let ``goal-range validation error renders in German`` () =
  let rg = ReadingRanges.defaults
  let errors = [ SystolicOutOfRange 999 ]
  let messages = Config.formatValidationErrors de rg errors
  test <@ messages = [ "Systolisch 999 liegt außerhalb des Bereichs (1–300)" ] @>

[<Fact>]
let ``German trend period labels render via LocalizedStrings.Trend`` () =
  test <@ TrendViews.renderPeriodLabel de ThisWeek = "Diese Woche" @>
  test <@ TrendViews.renderPeriodLabel de (CalendarWeek 22) = "KW 22" @>
