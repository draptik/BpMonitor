module LayoutViewTests

open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Web
open ViewTestHelpers

[<Fact>]
let ``layout renders a topbar with menu button, title and theme toggle`` () =
  let html = renderHtml (ReadingViews.landing defaultMember)

  test <@ html.Contains "class=\"topbar\"" @>
  test <@ html.Contains "for=\"nav-toggle\"" @>
  test <@ html.Contains "BpMonitor" @>
  test <@ html.Contains "class=\"theme-toggle\"" @>
  // the old single floating id-based toggle is gone
  test <@ not (html.Contains "id=\"theme-toggle\"") @>

[<Fact>]
let ``admin sees Members nav link`` () =
  let admin = { defaultMember with IsAdmin = true }
  let html = renderHtml (ReadingViews.landing admin)
  test <@ html.Contains $"href=\"{Routes.members}\"" @>

[<Fact>]
let ``non-admin does not see Members nav link`` () =
  let nonAdmin = { defaultMember with IsAdmin = false }
  let html = renderHtml (ReadingViews.landing nonAdmin)
  test <@ not (html.Contains $"href=\"{Routes.members}\"") @>

[<Fact>]
let ``every page has a BpMonitor footer`` () =
  let pages =
    [ renderHtml (ReadingViews.landing defaultMember)
      renderHtml (ReadingViews.history defaultMember "" [ sample ])
      renderHtml (ReadingViews.readingForm Routes.add "Me" true "Add reading" Routes.readings [] Binding.empty) ]

  for html in pages do
    test <@ html.Contains "<footer" @>
    test <@ html.Contains "BpMonitor" @>

[<Fact>]
let ``every authenticated page shows the logout button`` () =
  let pages =
    [ renderHtml (ReadingViews.landing defaultMember)
      renderHtml (ReadingViews.history defaultMember "" [ sample ])
      renderHtml (ReadingViews.readingForm Routes.add "Me" true "Add reading" Routes.readings [] Binding.empty) ]

  for html in pages do
    test <@ html.Contains $"action=\"{Routes.logout}\"" @>
    test <@ html.Contains "Logout" @>

[<Fact>]
let ``every authenticated page shows the member name`` () =
  let html = renderHtml (ReadingViews.landing defaultMember)
  test <@ html.Contains "Me" @>
