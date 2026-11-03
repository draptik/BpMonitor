module HistoryViewTests

open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Web
open ViewTestHelpers

let private historyHtml () =
  renderHtml (ReadingViews.history s defaultMember "" [ sample ] (Text.raw ""))

[<Fact>]
let ``history renders reading values and the chart container`` () =
  let html = historyHtml ()

  test <@ html.Contains "123" @>
  test <@ html.Contains "after walk" @>
  test <@ html.Contains "class=\"chart\"" @>

[<Fact>]
let ``history edit links return to the history page`` () =
  let html = historyHtml ()

  test <@ html.Contains(Routes.readingEdit 7 Routes.history) @>

[<Fact>]
let ``history marks its own nav link as the current page`` () =
  let html = historyHtml ()

  test <@ html.Contains $"href=\"{Routes.history}\" aria-current=\"page\"" @>

[<Fact>]
let ``reading edit button shows an icon with the label as tooltip`` () =
  let html = historyHtml ()

  let label = $"Edit reading from {Formats.formatLocal sample.Timestamp}"
  // Sized like the members page's Edit action, inside the row's actions cell
  test <@ html.Contains "<td class=\"reading-actions\"><a" @>

  test
    <@
      html.Contains
        $"role=\"button\" class=\"outline secondary edit-button\" title=\"{label}\" aria-label=\"{label}\"><svg"
    @>

[<Fact>]
let ``history chart section is a collapsible that persists its open state`` () =
  let html = historyHtml ()

  test <@ html.Contains "<details class=\"collapsible\" data-persist-key=\"history-chart\">" @>

[<Fact>]
let ``history readings table sits in a collapsible that persists its open state`` () =
  let html = historyHtml ()

  test
    <@
      html.Contains
        $"<details class=\"collapsible readings-section\" data-persist-key=\"history-readings\"><summary>{s.Reading.ReadingsSection}</summary><div id=\"readings\""
    @>

[<Fact>]
let ``edit form is prefilled from the reading`` () =
  let html =
    renderHtml (
      ReadingViews.readingForm s defaultMember (ReadingViews.editPage s 7 Routes.history) [] (Binding.ofReading sample)
    )

  test <@ html.Contains "name=\"Systolic\" value=\"123\"" @>
  test <@ html.Contains $"action=\"{Routes.readingUpdate 7}\"" @>
  test <@ html.Contains "after walk" @>

[<Fact>]
let ``edit form carries its return target and cancels back to it`` () =
  let html =
    renderHtml (
      ReadingViews.readingForm s defaultMember (ReadingViews.editPage s 7 Routes.recent) [] (Binding.ofReading sample)
    )

  test <@ html.Contains $"type=\"hidden\" name=\"{FormFields.returnTo}\" value=\"{Routes.recent}\"" @>
  test <@ html.Contains $"href=\"{Routes.recent}\" role=\"button\" class=\"secondary\"" @>

[<Fact>]
let ``edit form encodes its cancel href`` () =
  let html =
    renderHtml (
      ReadingViews.readingForm s defaultMember (ReadingViews.editPage s 7 "/recent\"x") [] (Binding.ofReading sample)
    )

  test <@ html.Contains "href=\"/recent&quot;x\" role=\"button\"" @>

[<Fact>]
let ``add form carries no return target`` () =
  let html =
    renderHtml (ReadingViews.readingForm s defaultMember (ReadingViews.addPage s) [] Binding.empty)

  test <@ not (html.Contains $"name=\"{FormFields.returnTo}\"") @>

[<Fact>]
let ``add form autofocuses the systolic field`` () =
  let html =
    renderHtml (ReadingViews.readingForm s defaultMember (ReadingViews.addPage s) [] Binding.empty)

  test <@ html.Contains "id=\"Systolic\" name=\"Systolic\" value=\"\" autofocus=\"autofocus\"" @>

[<Fact>]
let ``edit form does not autofocus any field`` () =
  let html =
    renderHtml (
      ReadingViews.readingForm s defaultMember (ReadingViews.editPage s 7 Routes.history) [] (Binding.ofReading sample)
    )

  test <@ not (html.Contains "autofocus") @>

[<Fact>]
let ``form renders the validation errors it is given`` () =
  let errors = [ "Systolic 999 is out of range (1–300)" ]

  let html =
    renderHtml (ReadingViews.readingForm s defaultMember (ReadingViews.addPage s) errors Binding.empty)

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
      ReadingViews.readingForm s defaultMember (ReadingViews.editPage s 7 Routes.history) [] (Binding.ofReading reading)
    )

  test <@ html.Contains "value=\"x&quot; onmouseover=&quot;alert(1)\"" @>
