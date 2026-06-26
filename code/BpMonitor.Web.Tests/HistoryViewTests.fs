module HistoryViewTests

open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Web
open ViewTestHelpers

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
