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
  test <@ BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh [] = "" @>

[<Fact>]
let ``toHtmlMedications uses Name as the row label`` () =
  let meds =
    [ medication 1 "HCTZ" (Some "hydrochlorothiazide") None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("\"ticktext\":[\"HCTZ") @>

[<Fact>]
let ``toHtmlMedications hover falls back to Name when FullName is absent`` () =
  let meds = [ medication 1 "lisinopril" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("lisinopril<br>") @>

[<Fact>]
let ``toHtmlMedications hover uses FullName when present`` () =
  let meds =
    [ medication 1 "HCTZ" (Some "hydrochlorothiazide") None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("hydrochlorothiazide<br>") @>

[<Fact>]
let ``toHtmlMedications extends an ongoing medication's bar to rangeHigh`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains(rangeHigh) @>

[<Fact>]
let ``toHtmlMedications hover uses the localized ongoing text for a medication with no EndDate`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.de.Charts false rangeLow rangeHigh meds

  test <@ html.Contains(LocalizedStrings.de.Charts.Ongoing) @>
  test <@ html.Contains("ongoing") |> not @>

[<Fact>]
let ``toHtmlMedications ends a completed medication's bar at its EndDate`` () =
  let meds =
    [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) (Some(DateOnly(2026, 1, 20))) ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("2026-01-20 00:00") @>

[<Fact>]
let ``toHtmlMedications renders a spike (scrubber) when showScrubber is true`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts true rangeLow rangeHigh meds

  test <@ html.Contains("\"showspikes\":true") @>

[<Fact>]
let ``toHtmlMedications does not render a spike when showScrubber is false`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("\"showspikes\":true") |> not @>

[<Fact>]
let ``toHtmlMedications assigns the same medication name the same color across renders`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html1 =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  let html2 =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  let color (html: string) =
    let marker = "\"line\":{\"color\":\""
    let start = html.IndexOf(marker) + marker.Length
    html.Substring(start, 7)

  test <@ color html1 = color html2 @>

[<Fact>]
let ``toHtmlMedications gives two different medications different colors`` () =
  let meds =
    [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None
      medication 2 "lisinopril" None None (DateOnly(2026, 1, 10)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  let marker = "\"line\":{\"color\":\""

  let firstColor =
    let start = html.IndexOf(marker) + marker.Length
    html.Substring(start, 7)

  let secondColor =
    let start = html.IndexOf(marker, html.IndexOf(marker) + 1) + marker.Length
    html.Substring(start, 7)

  test <@ firstColor <> secondColor @>

[<Fact>]
let ``toHtmlMedications adds a fill-based hover target spanning the whole bar`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("\"fill\":\"toself\"") @>
  test <@ html.Contains("\"hoveron\":\"fills\"") @>

[<Fact>]
let ``toHtmlMedications tints the hover tooltip's background with the medication's own color`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("\"hoverlabel\":{\"bgcolor\":\"#494195\"") @>

[<Fact>]
let ``toHtmlMedications skips native hover on the visible line trace`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("\"hoverinfo\":\"skip\"") @>

[<Fact>]
let ``toHtmlMedications disables the y-axis zeroline`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("\"zeroline\":false") @>

[<Fact>]
let ``toHtmlMedications adds spacing between y-axis tick labels and the axis`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("\"ticktext\":[\"HCTZ  \"]") @>

[<Fact>]
let ``toHtmlMedications carries light and dark colors in trace meta`` () =
  let meds = [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  test <@ html.Contains("\"meta\":\"#") @>
  test <@ html.Contains("|#") @>

[<Fact>]
let ``toHtmlMedications gives a restarted medication (same name, two rows) the same color`` () =
  let meds =
    [ medication 1 "HCTZ" None None (DateOnly(2026, 1, 5)) (Some(DateOnly(2026, 1, 20)))
      medication 2 "HCTZ" None None (DateOnly(2026, 1, 25)) None ]

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

  let marker = "\"line\":{\"color\":\""
  let firstIdx = html.IndexOf(marker)
  let secondIdx = html.IndexOf(marker, firstIdx + 1)
  let color at = html.Substring(at + marker.Length, 7)
  test <@ color firstIdx = color secondIdx @>

[<Fact>]
let ``toHtmlMedications never assigns a palette slot with identical light and dark colors`` () =
  let metaOf (name: string) =
    let meds = [ medication 1 name None None (DateOnly(2026, 1, 5)) None ]

    let html =
      BpChart.toHtmlMedications LocalizedStrings.en.Charts false rangeLow rangeHigh meds

    let marker = "\"meta\":\""
    let start = html.IndexOf(marker) + marker.Length
    html.Substring(start, html.IndexOf('"', start) - start)

  let distinctMetas =
    [ 0..99 ] |> List.map (fun i -> metaOf $"Medication{i}") |> List.distinct

  test <@ distinctMetas.Length >= 5 @>

  test
    <@
      distinctMetas
      |> List.forall (fun m -> let parts = m.Split('|') in parts[0] <> parts[1])
    @>

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

  let html =
    BpChart.toHtmlMedications LocalizedStrings.en.Charts true rangeLow rangeHigh meds

  verifyHtml html
