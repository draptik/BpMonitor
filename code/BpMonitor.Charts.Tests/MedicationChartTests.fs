module MedicationChartTests

open System
open System.Threading.Tasks
open BpMonitor.TestSupport
open Xunit
open Swensen.Unquote
open BpMonitor.Core
open BpMonitor.Charts

let private thisFile = IO.Path.Combine(__SOURCE_DIRECTORY__, __SOURCE_FILE__)
let private verifyHtml = Verifier.verifyHtml thisFile

let private medication id name fullName comment (startDate: DateOnly) (endDate: DateOnly option) : Medication =
  { Id = id
    MemberId = 1
    Name = name
    FullName = fullName
    Comment = comment
    StartDate = startDate
    EndDate = endDate
    CreatedAt = DateTimeOffset.MinValue
    ModifiedAt = DateTimeOffset.MinValue }

let private rangeLow = "2026-01-01 00:00"
let private rangeHigh = "2026-02-01 00:00"

[<Fact>]
let ``toHtmlMedications returns empty string when medications is empty`` () =
  test <@ BpChart.toHtmlMedications false rangeLow rangeHigh [] = "" @>

[<Fact>]
let ``toHtmlMedications uses Name as the row label`` () =
  let meds =
    [ medication 1 "HCTZ" (Some "hydrochlorothiazide") None (DateOnly(2026, 1, 5)) None ]

  let html = BpChart.toHtmlMedications false rangeLow rangeHigh meds
  test <@ html.Contains("\"HCTZ\"") @>

[<Fact>]
let ``toHtmlMedications hover falls back to Name when FullName is absent`` () =
  let meds = [ medication 1 "lisinopril" None None (DateOnly(2026, 1, 5)) None ]
  let html = BpChart.toHtmlMedications false rangeLow rangeHigh meds
  test <@ html.Contains("lisinopril<br>") @>

[<Fact>]
let ``toHtmlMedications hover uses FullName when present`` () =
  let meds =
    [ medication 1 "HCTZ" (Some "hydrochlorothiazide") None (DateOnly(2026, 1, 5)) None ]

  let html = BpChart.toHtmlMedications false rangeLow rangeHigh meds
  test <@ html.Contains("hydrochlorothiazide<br>") @>

[<Fact>]
let ``toHtmlMedications extends an ongoing medication's bar to rangeHigh`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]
  let html = BpChart.toHtmlMedications false rangeLow rangeHigh meds
  test <@ html.Contains(rangeHigh) @>

[<Fact>]
let ``toHtmlMedications ends a completed medication's bar at its EndDate`` () =
  let meds =
    [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) (Some(DateOnly(2026, 1, 20))) ]

  let html = BpChart.toHtmlMedications false rangeLow rangeHigh meds
  test <@ html.Contains("2026-01-20 00:00") @>

[<Fact>]
let ``toHtmlMedications renders a spike (scrubber) when showScrubber is true`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]
  let html = BpChart.toHtmlMedications true rangeLow rangeHigh meds
  test <@ html.Contains("\"showspikes\":true") @>

[<Fact>]
let ``toHtmlMedications does not render a spike when showScrubber is false`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]
  let html = BpChart.toHtmlMedications false rangeLow rangeHigh meds
  test <@ html.Contains("\"showspikes\":true") |> not @>

[<Fact>]
let ``toHtmlMedications matches snapshot`` () : Task =
  let meds =
    [ medication 1 "HCTZ" (Some "hydrochlorothiazide") None (DateOnly(2026, 1, 1)) None
      medication
        2
        "lisinopril"
        None
        (Some "Ran out of medication")
        (DateOnly(2026, 1, 15))
        (Some(DateOnly(2026, 1, 28))) ]

  let html = BpChart.toHtmlMedications true rangeLow rangeHigh meds
  verifyHtml html
