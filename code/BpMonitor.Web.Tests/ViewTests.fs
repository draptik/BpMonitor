module ViewTests

open System
open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Web

let private sample =
  { Id = 7
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
let ``history renders reading values, chart iframe and nav links`` () =
  let html = renderHtml (Views.history [ sample ])

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

  let html = renderHtml (Views.history [ nasty ])

  test <@ not (html.Contains "<script>x</script>") @>
  test <@ html.Contains "&lt;script&gt;" @>
