module MedicationViewTests

open System
open Xunit
open Swensen.Unquote
open Falco.Markup
open BpMonitor.Core
open BpMonitor.Web
open ViewTestHelpers

let private sampleMedication: Medication =
  { Id = 5
    MemberId = 1
    Name = "HCTZ"
    FullName = Some "Hydrochlorothiazide"
    Comment = None
    StartDate = DateOnly(2026, 1, 1)
    EndDate = None
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

[<Fact>]
let ``medicationsSection center-aligns the Start and End columns`` () =
  let html =
    renderHtml (Elem.div [] (MedicationViews.medicationsSection [ sampleMedication ] []))

  test <@ html.Contains "class=\"text-center\"" @>

[<Fact>]
let ``medicationsSection renders Edit as a button, matching Delete's style`` () =
  let html =
    renderHtml (Elem.div [] (MedicationViews.medicationsSection [ sampleMedication ] []))

  test <@ html.Contains $"<a href=\"{Routes.medicationEdit 5}\" role=\"button\" class=\"outline secondary\">Edit</a>" @>
