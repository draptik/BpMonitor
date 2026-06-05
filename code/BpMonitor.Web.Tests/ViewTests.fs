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
  let html = renderHtml Views.landing

  test <@ html.Contains "href=\"/add\"" @>
  test <@ html.Contains "href=\"/history\"" @>
  // the Home nav link is marked active on the landing page
  test <@ html.Contains "href=\"/\" aria-current=\"page\"" @>

[<Fact>]
let ``every page has a BpMonitor footer`` () =
  let pages =
    [ renderHtml Views.landing
      renderHtml (Views.history defaultMember [ sample ])
      renderHtml (Views.readingForm "/add" "Add reading" "/readings" [] Binding.empty) ]

  for html in pages do
    test <@ html.Contains "<footer" @>
    test <@ html.Contains "BpMonitor" @>

[<Fact>]
let ``history renders reading values, chart iframe and nav links`` () =
  let html = renderHtml (Views.history defaultMember [ sample ])

  test <@ html.Contains "123" @>
  test <@ html.Contains "after walk" @>
  test <@ html.Contains "<iframe" && html.Contains "src=\"/chart\"" @>
  test <@ html.Contains "href=\"/add\"" @>
  test <@ html.Contains "/readings/7/edit" @>
  // the History nav link is marked active on the history page
  test <@ html.Contains "href=\"/history\" aria-current=\"page\"" @>

[<Fact>]
let ``edit form is prefilled from the reading`` () =
  let html =
    renderHtml (Views.readingForm "" "Edit reading" "/readings/7" [] (Binding.ofReading sample))

  test <@ html.Contains "name=\"Systolic\" value=\"123\"" @>
  test <@ html.Contains "action=\"/readings/7\"" @>
  test <@ html.Contains "after walk" @>

[<Fact>]
let ``form renders the validation errors it is given`` () =
  let errors = [ "Systolic 999 is out of range (1–300)" ]

  let html =
    renderHtml (Views.readingForm "/add" "Add reading" "/readings" errors Binding.empty)

  test <@ html.Contains "errors" @>
  test <@ html.Contains "out of range" @>

[<Fact>]
let ``view encodes user-supplied content`` () =
  let nasty =
    { sample with
        Comments = Some "<script>x</script>" }

  let html = renderHtml (Views.history defaultMember [ nasty ])

  test <@ not (html.Contains "<script>x</script>") @>
  test <@ html.Contains "&lt;script&gt;" @>

[<Fact>]
let ``members page renders Admin and Active columns and Edit link`` () =
  let otherMember =
    { Id = 2
      Name = "Alice"
      IsAdmin = false
      IsActive = true
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html = renderHtml (Views.members [ defaultMember; otherMember ] defaultMember)

  test <@ html.Contains "Admin" @>
  test <@ html.Contains "Active" @>
  test <@ html.Contains "href=\"/members/1/edit\"" @>
  test <@ html.Contains "href=\"/members/2/edit\"" @>

[<Fact>]
let ``memberForm prefills name and reflects IsAdmin and IsActive`` () =
  let m =
    { Id = 3
      Name = "Bob"
      IsAdmin = true
      IsActive = false
      CreatedAt = DateTimeOffset.MinValue
      ModifiedAt = DateTimeOffset.MinValue }

  let html = renderHtml (Views.memberForm "/members" "Edit member" "/members/3" [] m)

  test <@ html.Contains "value=\"Bob\"" @>
  test <@ html.Contains "action=\"/members/3\"" @>
  // IsAdmin checked → checked attribute present
  test <@ html.Contains "checked" @>

[<Fact>]
let ``memberForm renders errors`` () =
  let html =
    renderHtml (
      Views.memberForm
        "/members"
        "Edit member"
        "/members/3"
        [ "At least one member must be an active admin" ]
        defaultMember
    )

  test <@ html.Contains "active admin" @>
