module MedicationViewTests

open System
open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Web
open ViewTestHelpers

let private sampleMedication: Medication =
  {
    Id = 5
    MemberId = 1
    Name = "HCTZ"
    FullName = Some "Hydrochlorothiazide"
    Comment = None
    StartDate = DateOnly(2026, 1, 1)
    EndDate = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue
  }

[<Fact>]
let ``medicationsSection center-aligns the Start and End columns`` () =
  let html =
    renderHtml (Elem.div [] (MedicationViews.medicationsSection s [ sampleMedication ] []))

  test <@ html.Contains "class=\"text-center\"" @>

[<Fact>]
let ``medicationsSection renders Edit as an outline secondary button`` () =
  let html =
    renderHtml (Elem.div [] (MedicationViews.medicationsSection s [ sampleMedication ] []))

  test <@ html.Contains $"<a href=\"{Routes.medicationEdit 5}\" role=\"button\" class=\"outline secondary\">Edit</a>" @>

[<Fact>]
let ``medicationsSection renders Delete with a danger class, unlike Edit`` () =
  let html =
    renderHtml (Elem.div [] (MedicationViews.medicationsSection s [ sampleMedication ] []))

  test <@ html.Contains "class=\"outline button-danger\"" @>

[<Fact>]
let ``medicationsSection wraps the section in a collapsible details element`` () =
  let html =
    renderHtml (Elem.div [] (MedicationViews.medicationsSection s [ sampleMedication ] []))

  test <@ html.Contains "<details" @>
  test <@ html.Contains "data-persist-key=\"settings-medications\"" @>
  test <@ html.Contains "<summary>" @>

[<Fact>]
let ``medicationsSection marks the required and optional add-medication fields`` () =
  let html =
    renderHtml (Elem.div [] (MedicationViews.medicationsSection s [ sampleMedication ] []))

  test <@ html.Contains "field-required" @>
  test <@ html.Contains "field-optional" @>

[<Fact>]
let ``medicationsSection asks for confirmation before deleting, naming the medication`` () =
  let html =
    renderHtml (Elem.div [] (MedicationViews.medicationsSection s [ sampleMedication ] []))

  test <@ html.Contains "hx-confirm=\"Delete HCTZ? This cannot be undone.\"" @>

[<Fact>]
let ``medicationForm encodes quotes in a field's value attribute`` () =
  let html =
    renderHtml (
      MedicationViews.medicationForm
        s
        "Me"
        true
        "Edit medication"
        (Routes.medicationUpdate 5)
        []
        "HCTZ"
        "x\" onmouseover=\"alert(1)"
        ""
        "01.01.2026"
        ""
    )

  test <@ html.Contains "value=\"x&quot; onmouseover=&quot;alert(1)\"" @>

[<Fact>]
let ``medicationsSection encodes quotes in the delete confirmation`` () =
  let m =
    { sampleMedication with
        Name = "x\" onmouseover=\"alert(1)"
    }

  let html = renderHtml (Elem.div [] (MedicationViews.medicationsSection s [ m ] []))

  test <@ html.Contains "hx-confirm=\"Delete x&quot; onmouseover=&quot;alert(1)? This cannot be undone.\"" @>
