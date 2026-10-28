module LayoutViewTests

open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Web
open ViewTestHelpers

[<Fact>]
let ``layout renders a topbar with menu button, title and theme toggle`` () =
  let html = renderHtml (ReadingViews.landing s defaultMember)

  test <@ html.Contains "class=\"topbar\"" @>
  test <@ html.Contains "for=\"nav-toggle\"" @>
  test <@ html.Contains "BpMonitor" @>
  test <@ html.Contains "class=\"theme-toggle\"" @>
  // the old single floating id-based toggle is gone
  test <@ not (html.Contains "id=\"theme-toggle\"") @>

[<Fact>]
let ``theme toggle renders a sun and a moon icon with localized tooltips per theme`` () =
  let pages =
    [
      renderHtml (ReadingViews.landing s defaultMember)
      renderHtml (LoginViews.loginMember s defaultMember [])
    ]

  for html in pages do
    // light is the default theme, so its tooltip is the server-side fallback for when JS doesn't run
    test <@ html.Contains "title=\"Switch to dark theme\" aria-label=\"Switch to dark theme\"" @>
    test <@ html.Contains "data-tooltip-light=\"Switch to dark theme\"" @>
    test <@ html.Contains "data-tooltip-dark=\"Switch to light theme\"" @>
    test <@ html.Contains "<svg class=\"svg-icon theme-icon-light\"" @>
    test <@ html.Contains "<svg class=\"svg-icon theme-icon-dark\"" @>

[<Fact>]
let ``admin sees Members nav link`` () =
  let admin = { defaultMember with IsAdmin = true }
  let html = renderHtml (ReadingViews.landing s admin)
  test <@ html.Contains $"href=\"{Routes.members}\"" @>

[<Fact>]
let ``non-admin does not see Members nav link`` () =
  let nonAdmin = { defaultMember with IsAdmin = false }
  let html = renderHtml (ReadingViews.landing s nonAdmin)
  test <@ not (html.Contains $"href=\"{Routes.members}\"") @>

[<Fact>]
let ``sidebar nav links appear in order: Add, Recent, Trends, History, Export JSON, Export CSV, Settings, Members`` () =
  let admin = { defaultMember with IsAdmin = true }
  let html = renderHtml (ReadingViews.landing s admin)

  let indexOf (href: string) = html.IndexOf $"href=\"{href}\""

  let indices =
    [
      Routes.add
      Routes.recent
      Routes.trends
      Routes.history
      Routes.exportJson
      Routes.exportCsv
      Routes.settings
      Routes.members
    ]
    |> List.map indexOf

  test <@ indices |> List.forall (fun i -> i >= 0) @>
  test <@ indices = List.sort indices @>

[<Fact>]
let ``every page has a BpMonitor footer`` () =
  let pages =
    [
      renderHtml (ReadingViews.landing s defaultMember)
      renderHtml (ReadingViews.history s defaultMember "" [ sample ] (Text.raw ""))
      renderHtml (ReadingViews.readingForm s defaultMember (ReadingViews.addPage s) [] Binding.empty)
    ]

  for html in pages do
    test <@ html.Contains "<footer" @>
    test <@ html.Contains "BpMonitor" @>

[<Fact>]
let ``every authenticated page shows the logout button`` () =
  let pages =
    [
      renderHtml (ReadingViews.landing s defaultMember)
      renderHtml (ReadingViews.history s defaultMember "" [ sample ] (Text.raw ""))
      renderHtml (ReadingViews.readingForm s defaultMember (ReadingViews.addPage s) [] Binding.empty)
    ]

  for html in pages do
    test <@ html.Contains $"action=\"{Routes.logout}\"" @>
    test <@ html.Contains "Logout" @>

[<Fact>]
let ``logout button shows an icon with the label as tooltip`` () =
  let html = renderHtml (ReadingViews.landing s defaultMember)

  test <@ html.Contains "<button type=\"submit\" class=\"icon-button\" title=\"Logout\" aria-label=\"Logout\"><svg" @>

[<Fact>]
let ``icon button labels are HTML-encoded in attributes`` () =
  let quoted =
    { s with
        Shell =
          { s.Shell with
              Logout = "Log \"out\" & go"
              SwitchToDarkTheme = "Go \"dark\" & go"
              SwitchToLightTheme = "Go \"light\" & go"
          }
        Table =
          { s.Table with
              EditReading = fun _ -> "Edit \"it\" & go"
          }
    }

  let html =
    renderHtml (ReadingViews.history quoted defaultMember "" [ sample ] (Text.raw ""))

  test <@ html.Contains "title=\"Log &quot;out&quot; &amp; go\" aria-label=\"Log &quot;out&quot; &amp; go\"" @>
  test <@ html.Contains "title=\"Edit &quot;it&quot; &amp; go\" aria-label=\"Edit &quot;it&quot; &amp; go\"" @>
  test <@ html.Contains "title=\"Go &quot;dark&quot; &amp; go\" aria-label=\"Go &quot;dark&quot; &amp; go\"" @>
  test <@ html.Contains "data-tooltip-dark=\"Go &quot;light&quot; &amp; go\"" @>

[<Fact>]
let ``every authenticated page shows the member name`` () =
  let html = renderHtml (ReadingViews.landing s defaultMember)
  test <@ html.Contains "Me" @>

[<Fact>]
let ``page title HTML-encodes member name to prevent title injection`` () =
  let hostile =
    { defaultMember with
        Name = "</title><script>alert(1)</script>"
    }

  let html = renderHtml (LoginViews.loginMember s hostile [])

  // The raw injection string must not appear (it would break out of the <title> element)
  test <@ not (html.Contains "</title><script>") @>
  // The HTML-encoded form must appear instead
  test <@ html.Contains "&lt;/title&gt;" @>
