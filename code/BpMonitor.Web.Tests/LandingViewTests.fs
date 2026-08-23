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
  let html = renderHtml (ReadingViews.landing s defaultMember)

  test <@ html.Contains $"href=\"{Routes.add}\"" @>
  test <@ html.Contains $"href=\"{Routes.history}\"" @>
  // the topbar title links to the landing page (replaces the removed Home sidebar entry)
  test <@ html.Contains $"class=\"topbar-title\" href=\"{Routes.home}\"" @>

[<Fact>]
let ``landing renders action buttons for trends, recent, settings and both exports`` () =
  let html = renderHtml (ReadingViews.landing s defaultMember)

  test <@ occurrences $"href=\"{Routes.trends}\"" html = 2 @>
  test <@ occurrences $"href=\"{Routes.recent}\"" html = 2 @>
  test <@ occurrences $"href=\"{Routes.settings}\"" html = 2 @>
  test <@ occurrences $"href=\"{Routes.exportJson}\"" html = 2 @>
  test <@ occurrences $"href=\"{Routes.exportCsv}\"" html = 2 @>

[<Fact>]
let ``landing export action buttons do not get hx-boosted`` () =
  let html = renderHtml (ReadingViews.landing s defaultMember)
  // the sidebar already carries hx-boost="false" on its two export links;
  // the landing action buttons must add two more, not rely on the sidebar.
  test <@ occurrences "hx-boost=\"false\"" html = 4 @>

[<Fact>]
let ``admin sees a Members action button on landing`` () =
  let admin = { defaultMember with IsAdmin = true }
  let html = renderHtml (ReadingViews.landing s admin)
  test <@ occurrences $"href=\"{Routes.members}\"" html = 2 @>

[<Fact>]
let ``non-admin does not see a Members action button on landing`` () =
  let nonAdmin = { defaultMember with IsAdmin = false }
  let html = renderHtml (ReadingViews.landing s nonAdmin)
  test <@ occurrences $"href=\"{Routes.members}\"" html = 0 @>

[<Fact>]
let ``landing renders two home-actions groups`` () =
  let html = renderHtml (ReadingViews.landing s defaultMember)
  test <@ occurrences "class=\"home-actions" html = 2 @>

[<Fact>]
let ``landing action buttons appear in order: Add, Recent, Trends, History, Export JSON, Export CSV, Settings, Members``
  ()
  =
  let admin = { defaultMember with IsAdmin = true }
  let html = renderHtml (ReadingViews.landing s admin)
  // the sidebar renders first with the same hrefs, so anchor to the landing
  // action grid by slicing from its first container onward
  let landingHtml = html.Substring(html.IndexOf "class=\"home-actions\"")

  let indexOf (href: string) = landingHtml.IndexOf $"href=\"{href}\""

  let indices =
    [ Routes.add
      Routes.recent
      Routes.trends
      Routes.history
      Routes.exportJson
      Routes.exportCsv
      Routes.settings
      Routes.members ]
    |> List.map indexOf

  test <@ indices |> List.forall (fun i -> i >= 0) @>
  test <@ indices = List.sort indices @>
