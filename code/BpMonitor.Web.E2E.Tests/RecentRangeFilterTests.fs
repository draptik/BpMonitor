module BpMonitor.Web.E2E.RecentRangeFilterTests

open System
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// recent-scrubber.js's plotly_relayout handler hides value-strip cells outside the
/// chart's visible x-range; these cover range events it used to misread.
type RecentRangeFilterTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  // Plotly drops a midnight bound's time part ("2026-10-05"); it must be read in the
  // same zone as the "yyyy-MM-dd HH:mm" data-x keys.
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

/// Separate class: each class claims its own member, and a setup code works only once.
type RecentOneSidedRangeTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``a one-sided range update keeps readings outside the range hidden``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName fixture.MemberSetupCode page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", DateTime.Now.AddDays(-20.0).ToString("yyyy-MM-dd HH:mm"))
      do! page.FillAsync("#Systolic", "144")
      do! page.FillAsync("#Diastolic", "76")
      do! page.FillAsync("#HeartRate", "62")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")

      let cell =
        page.Locator(".value-strip td.value-strip-value", PageLocatorOptions(HasText = "144"))

      let outOfRange = Text.RegularExpressions.Regex("out-of-range")

      let! _ = page.ClickAsync("button:text('Last 7 days')")
      do! Assertions.Expect(cell).ToHaveClassAsync(outOfRange)

      // Moves only the start bound; the 20-day-old reading is still outside the range.
      // Read back in the same call: a later relayout (e.g. theme.js) would re-derive the range and mask it.
      let lo = DateTime.Now.AddDays(-6.0).ToString("yyyy-MM-dd HH:mm")

      let! classAfter =
        page.EvaluateAsync<string>(
          "async lo => { await Plotly.relayout(document.querySelector('.js-plotly-plot'), { 'xaxis.range[0]': lo });"
          + " return [...document.querySelectorAll('.value-strip td.value-strip-value')].find(c => c.textContent === '144').className; }",
          lo
        )

      Assert.Contains("out-of-range", classAfter)
    }
