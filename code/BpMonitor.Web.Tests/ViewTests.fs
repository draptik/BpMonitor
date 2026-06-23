module ViewTests

open System
open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Web

let private defaultMember: FamilyMember =
  { Id = 1
    Name = "Me"
    IsAdmin = true
    IsActive = true
    PasswordHash = None
    Goal = GoalRange.defaults
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let private sample =
  { Id = 7
    MemberId = 1
    Systolic = 123
    Diastolic = 81
    HeartRate = 67
    Timestamp = Timestamp.utc 2026 5 1 9 0 0
    Comments = Some "after walk"
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

[<Fact>]
let ``landing renders links to add and history`` () =
  let html = renderHtml (ReadingViews.landing defaultMember)

  test <@ html.Contains "href=\"/add\"" @>
  test <@ html.Contains "href=\"/history\"" @>
  // the Home nav link is marked active on the landing page
  test <@ html.Contains "href=\"/\" aria-current=\"page\"" @>

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
let ``landing renders action buttons for trends, recent, settings and both exports`` () =
  let html = renderHtml (ReadingViews.landing defaultMember)

  test <@ occurrences "href=\"/trends\"" html = 2 @>
  test <@ occurrences "href=\"/recent\"" html = 2 @>
  test <@ occurrences "href=\"/settings\"" html = 2 @>
  test <@ occurrences "href=\"/export\"" html = 2 @>
  test <@ occurrences "href=\"/export.csv\"" html = 2 @>

[<Fact>]
let ``landing export action buttons do not get hx-boosted`` () =
  let html = renderHtml (ReadingViews.landing defaultMember)
  // the sidebar already carries hx-boost="false" on its two export links;
  // the landing action buttons must add two more, not rely on the sidebar's.
  test <@ occurrences "hx-boost=\"false\"" html = 4 @>

[<Fact>]
let ``admin sees a Members action button on landing`` () =
  let admin = { defaultMember with IsAdmin = true }
  let html = renderHtml (ReadingViews.landing admin)
  test <@ occurrences "href=\"/members\"" html = 2 @>

[<Fact>]
let ``non-admin does not see a Members action button on landing`` () =
  let nonAdmin = { defaultMember with IsAdmin = false }
  let html = renderHtml (ReadingViews.landing nonAdmin)
  test <@ occurrences "href=\"/members\"" html = 0 @>

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
  test <@ html.Contains "href=\"/members\"" @>

[<Fact>]
let ``non-admin does not see Members nav link`` () =
  let nonAdmin = { defaultMember with IsAdmin = false }
  let html = renderHtml (ReadingViews.landing nonAdmin)
  test <@ not (html.Contains "href=\"/members\"") @>

[<Fact>]
let ``every page has a BpMonitor footer`` () =
  let pages =
    [ renderHtml (ReadingViews.landing defaultMember)
      renderHtml (ReadingViews.history defaultMember "" [ sample ])
      renderHtml (ReadingViews.readingForm "/add" "Me" true "Add reading" "/readings" [] Binding.empty) ]

  for html in pages do
    test <@ html.Contains "<footer" @>
    test <@ html.Contains "BpMonitor" @>

[<Fact>]
let ``every authenticated page shows the logout button`` () =
  let pages =
    [ renderHtml (ReadingViews.landing defaultMember)
      renderHtml (ReadingViews.history defaultMember "" [ sample ])
      renderHtml (ReadingViews.readingForm "/add" "Me" true "Add reading" "/readings" [] Binding.empty) ]

  for html in pages do
    test <@ html.Contains "action=\"/logout\"" @>
    test <@ html.Contains "Logout" @>

[<Fact>]
let ``every authenticated page shows the member name`` () =
  let html = renderHtml (ReadingViews.landing defaultMember)
  test <@ html.Contains "Me" @>

[<Fact>]
let ``history renders reading values, chart div and nav links`` () =
  let html = renderHtml (ReadingViews.history defaultMember "" [ sample ])

  test <@ html.Contains "123" @>
  test <@ html.Contains "after walk" @>
  test <@ html.Contains "class=\"chart\"" @>
  test <@ html.Contains "href=\"/add\"" @>
  test <@ html.Contains "/readings/7/edit" @>
  // the History nav link is marked active on the history page
  test <@ html.Contains "href=\"/history\" aria-current=\"page\"" @>

[<Fact>]
let ``edit form is prefilled from the reading`` () =
  let html =
    renderHtml (ReadingViews.readingForm "" "Me" true "Edit reading" "/readings/7" [] (Binding.ofReading sample))

  test <@ html.Contains "name=\"Systolic\" value=\"123\"" @>
  test <@ html.Contains "action=\"/readings/7\"" @>
  test <@ html.Contains "after walk" @>

[<Fact>]
let ``form renders the validation errors it is given`` () =
  let errors = [ "Systolic 999 is out of range (1–300)" ]

  let html =
    renderHtml (ReadingViews.readingForm "/add" "Me" true "Add reading" "/readings" errors Binding.empty)

  test <@ html.Contains "errors" @>
  test <@ html.Contains "out of range" @>

[<Fact>]
let ``view encodes user-supplied content`` () =
  let nasty =
    { sample with
        Comments = Some "<script>x</script>" }

  let html = renderHtml (ReadingViews.history defaultMember "" [ nasty ])

  test <@ not (html.Contains "<script>x</script>") @>
  test <@ html.Contains "&lt;script&gt;" @>

[<Fact>]
let ``members page renders Admin and Active columns and Edit link`` () =
  let otherMember =
    { Id = 2
      Name = "Alice"
      IsAdmin = false
      IsActive = true
      PasswordHash = None
      Goal = GoalRange.defaults
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html =
    renderHtml (MemberViews.members [ defaultMember; otherMember ] defaultMember None)

  test <@ html.Contains "Admin" @>
  test <@ html.Contains "Active" @>
  test <@ html.Contains "href=\"/members/1/edit\"" @>
  test <@ html.Contains "href=\"/members/2/edit\"" @>

[<Fact>]
let ``members page shows claimed/unclaimed badge`` () =
  let claimed =
    { defaultMember with
        PasswordHash = Some "somehash" }

  let unclaimed =
    { Id = 2
      Name = "Alice"
      IsAdmin = false
      IsActive = true
      PasswordHash = None
      Goal = GoalRange.defaults
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html = renderHtml (MemberViews.members [ claimed; unclaimed ] claimed None)

  test <@ html.Contains "Claimed" @>
  test <@ html.Contains "Unclaimed" @>

[<Fact>]
let ``members page shows reset-password button`` () =
  let html = renderHtml (MemberViews.members [ defaultMember ] defaultMember None)
  test <@ html.Contains "reset-password" @>

[<Fact>]
let ``members page does NOT show Switch button`` () =
  let html = renderHtml (MemberViews.members [ defaultMember ] defaultMember None)
  test <@ not (html.Contains "/members/switch") @>

[<Fact>]
let ``memberForm prefills name and reflects IsAdmin and IsActive`` () =
  let m =
    { Id = 3
      Name = "Bob"
      IsAdmin = true
      IsActive = false
      PasswordHash = None
      Goal = GoalRange.defaults
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html =
    renderHtml (MemberViews.memberForm "/members" "Me" true "Edit member" "/members/3" [] m)

  test <@ html.Contains "value=\"Bob\"" @>
  test <@ html.Contains "action=\"/members/3\"" @>
  // IsAdmin checked → checked attribute present
  test <@ html.Contains "checked" @>

[<Fact>]
let ``memberForm renders errors`` () =
  let html =
    renderHtml (
      MemberViews.memberForm
        "/members"
        "Me"
        true
        "Edit member"
        "/members/3"
        [ "At least one member must be an active admin" ]
        defaultMember
    )

  test <@ html.Contains "active admin" @>

[<Fact>]
let ``loginPage renders sign-in form with username and password fields`` () =
  let html = renderHtml (LoginViews.loginPage [])

  test <@ html.Contains "Sign in" @>
  test <@ html.Contains "Username" @>
  test <@ html.Contains "Password" @>

[<Fact>]
let ``loginPage renders errors when provided`` () =
  let html = renderHtml (LoginViews.loginPage [ "Invalid name or password" ])

  test <@ html.Contains "Invalid name or password" @>

[<Fact>]
let ``loginMember shows claim form for unclaimed member`` () =
  let html = renderHtml (LoginViews.loginMember defaultMember [])

  test <@ html.Contains "PasswordConfirm" @>
  test <@ html.Contains "Claim account" @>

[<Fact>]
let ``loginMember shows password form for claimed member`` () =
  let claimed =
    { defaultMember with
        PasswordHash = Some "x" }

  let html = renderHtml (LoginViews.loginMember claimed [])

  test <@ not (html.Contains "PasswordConfirm") @>
  test <@ html.Contains "Login" @>

[<Fact>]
let ``loginMember renders errors`` () =
  let html =
    renderHtml (LoginViews.loginMember defaultMember [ "Passwords do not match" ])

  test <@ html.Contains "Passwords do not match" @>
