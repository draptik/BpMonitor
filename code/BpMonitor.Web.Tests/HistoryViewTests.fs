module HistoryViewTests

open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Web
open ViewTestHelpers

[<Fact>]
let ``history renders reading values, chart div and nav links`` () =
  let html =
    renderHtml (ReadingViews.history s defaultMember "" [ sample ] (Text.raw ""))

  test <@ html.Contains "123" @>
  test <@ html.Contains "after walk" @>
  test <@ html.Contains "class=\"chart\"" @>
  test <@ html.Contains $"href=\"{Routes.add}\"" @>
  test <@ html.Contains(Routes.readingEdit 7 Routes.history) @>
  // Edit renders as a Pico button sized like the members page's Edit action
  test <@ html.Contains "class=\"reading-actions\"" @>
  test <@ html.Contains "role=\"button\" class=\"outline secondary\"" @>
  // the History nav link is marked active on the history page
  test <@ html.Contains $"href=\"{Routes.history}\" aria-current=\"page\"" @>

[<Fact>]
let ``history chart section is a collapsible that persists its open state`` () =
  let html =
    renderHtml (ReadingViews.history s defaultMember "" [ sample ] (Text.raw ""))

  test <@ html.Contains "<details class=\"collapsible\" data-persist-key=\"history-chart\">" @>

[<Fact>]
let ``edit form is prefilled from the reading`` () =
  let html =
    renderHtml (
      ReadingViews.readingForm
        s
        ""
        "Me"
        true
        "Edit reading"
        (Routes.readingUpdate 7)
        (Some Routes.history)
        []
        (Binding.ofReading sample)
    )

  test <@ html.Contains "name=\"Systolic\" value=\"123\"" @>
  test <@ html.Contains $"action=\"{Routes.readingUpdate 7}\"" @>
  test <@ html.Contains "after walk" @>

[<Fact>]
let ``edit form carries its return target and cancels back to it`` () =
  let html =
    renderHtml (
      ReadingViews.readingForm
        s
        ""
        "Me"
        true
        "Edit reading"
        (Routes.readingUpdate 7)
        (Some Routes.recent)
        []
        (Binding.ofReading sample)
    )

  test <@ html.Contains $"type=\"hidden\" name=\"{FormFields.returnTo}\" value=\"{Routes.recent}\"" @>
  test <@ html.Contains $"href=\"{Routes.recent}\" role=\"button\" class=\"secondary\"" @>

[<Fact>]
let ``add form carries no return target`` () =
  let html =
    renderHtml (ReadingViews.readingForm s Routes.add "Me" true "Add reading" Routes.readings None [] Binding.empty)

  test <@ not (html.Contains $"name=\"{FormFields.returnTo}\"") @>

[<Fact>]
let ``add form autofocuses the systolic field`` () =
  let html =
    renderHtml (ReadingViews.readingForm s Routes.add "Me" true "Add reading" Routes.readings None [] Binding.empty)

  test <@ html.Contains "id=\"Systolic\" name=\"Systolic\" value=\"\" autofocus=\"autofocus\"" @>

[<Fact>]
let ``edit form does not autofocus any field`` () =
  let html =
    renderHtml (
      ReadingViews.readingForm
        s
        ""
        "Me"
        true
        "Edit reading"
        (Routes.readingUpdate 7)
        (Some Routes.history)
        []
        (Binding.ofReading sample)
    )

  test <@ not (html.Contains "autofocus") @>

[<Fact>]
let ``form renders the validation errors it is given`` () =
  let errors = [ "Systolic 999 is out of range (1–300)" ]

  let html =
    renderHtml (ReadingViews.readingForm s Routes.add "Me" true "Add reading" Routes.readings None errors Binding.empty)

  test <@ html.Contains "errors" @>
  test <@ html.Contains "out of range" @>

[<Fact>]
let ``view encodes user-supplied content`` () =
  let nasty =
    { sample with
        Comments = Some "<script>x</script>"
    }

  let html =
    renderHtml (ReadingViews.history s defaultMember "" [ nasty ] (Text.raw ""))

  test <@ not (html.Contains "<script>x</script>") @>
  test <@ html.Contains "&lt;script&gt;" @>

[<Fact>]
let ``edit form encodes quotes in the comment's value attribute`` () =
  let reading =
    { sample with
        Comments = Some "x\" onmouseover=\"alert(1)"
    }

  let html =
    renderHtml (
      ReadingViews.readingForm
        s
        ""
        "Me"
        true
        "Edit reading"
        (Routes.readingUpdate 7)
        (Some Routes.history)
        []
        (Binding.ofReading reading)
    )

  test <@ html.Contains "value=\"x&quot; onmouseover=&quot;alert(1)\"" @>
