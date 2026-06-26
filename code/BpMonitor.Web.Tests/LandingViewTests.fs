module LandingViewTests

open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Web
open ViewTestHelpers

/// Counts non-overlapping occurrences of `needle` in `haystack`. Used below because these
/// routes already appear once in the sidebar nav — the landing action buttons must add a
/// second occurrence, not merely reuse the sidebar's.
let private occurrences (needle: string) (haystack: string) : int =
  let rec count (start: int) (acc: int) =
    let idx = haystack.IndexOf(needle, start)

    if idx < 0 then
      acc
    else
      count (idx + needle.Length) (acc + 1)

  count 0 0

[<Fact>]
let ``landing renders links to add and history`` () =
  let html = renderHtml (ReadingViews.landing defaultMember)

  test <@ html.Contains $"href=\"{Routes.add}\"" @>
  test <@ html.Contains $"href=\"{Routes.history}\"" @>
  // the topbar title links to the landing page (replaces the removed Home sidebar entry)
  test <@ html.Contains $"class=\"topbar-title\" href=\"{Routes.home}\"" @>

[<Fact>]
let ``landing renders action buttons for trends, recent, settings and both exports`` () =
  let html = renderHtml (ReadingViews.landing defaultMember)

  test <@ occurrences $"href=\"{Routes.trends}\"" html = 2 @>
  test <@ occurrences $"href=\"{Routes.recent}\"" html = 2 @>
  test <@ occurrences $"href=\"{Routes.settings}\"" html = 2 @>
  test <@ occurrences $"href=\"{Routes.exportJson}\"" html = 2 @>
  test <@ occurrences $"href=\"{Routes.exportCsv}\"" html = 2 @>

[<Fact>]
let ``landing export action buttons do not get hx-boosted`` () =
  let html = renderHtml (ReadingViews.landing defaultMember)
  // the sidebar already carries hx-boost="false" on its two export links;
  // the landing action buttons must add two more, not rely on the sidebar.
  test <@ occurrences "hx-boost=\"false\"" html = 4 @>

[<Fact>]
let ``admin sees a Members action button on landing`` () =
  let admin = { defaultMember with IsAdmin = true }
  let html = renderHtml (ReadingViews.landing admin)
  test <@ occurrences $"href=\"{Routes.members}\"" html = 2 @>

[<Fact>]
let ``non-admin does not see a Members action button on landing`` () =
  let nonAdmin = { defaultMember with IsAdmin = false }
  let html = renderHtml (ReadingViews.landing nonAdmin)
  test <@ occurrences $"href=\"{Routes.members}\"" html = 0 @>
