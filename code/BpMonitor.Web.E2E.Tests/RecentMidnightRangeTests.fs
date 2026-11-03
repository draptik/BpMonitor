module BpMonitor.Web.E2E.RecentMidnightRangeTests

open System
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// Plotly drops a midnight range bound's time part ("2026-10-05"); the value strip's
/// range check must read it in the same zone as its "yyyy-MM-dd HH:mm" data-x keys.
type RecentMidnightRangeTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``a reading just after a midnight range start stays in view east of UTC``() : Task =
    task {
      use! traced =
        fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800), "Europe/Berlin")

      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName fixture.MemberSetupCode page

      let day = DateTime.Today.AddDays(-3.0)
      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", day.AddMinutes(30.0).ToString("yyyy-MM-dd HH:mm"))
      do! page.FillAsync("#Systolic", "133")
      do! page.FillAsync("#Diastolic", "76")
      do! page.FillAsync("#HeartRate", "62")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")

      let lo = day.ToString("yyyy-MM-dd")
      let hi = day.AddDays(1.0).ToString("yyyy-MM-dd") + " 12:00"

      let! _ =
        page.EvaluateAsync(
          "([lo, hi]) => Plotly.relayout(document.querySelector('.js-plotly-plot'), { 'xaxis.range': [lo, hi] })",
          [| lo; hi |]
        )

      let cell =
        page.Locator(".value-strip td.value-strip-value", PageLocatorOptions(HasText = "133"))

      do! Assertions.Expect(cell).Not.ToHaveClassAsync(Text.RegularExpressions.Regex("out-of-range"))
    }
