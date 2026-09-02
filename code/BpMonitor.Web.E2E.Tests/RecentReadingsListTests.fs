module BpMonitor.Web.E2E.RecentReadingsListTests

open System
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// The collapsible readings list below the chart narrows to the chart's visible x-range
/// (recent-scrubber.js's plotly_relayout handler), same mechanism as the value strip.
type RecentReadingsListTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``the readings list narrows on a zoom shortcut and widens back on an autorange reset``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName page

      let now = DateTime.Now

      // Distinct systolic values so the surviving row's identity can be checked, not just its count.
      let readings = [ 3.0, 111; 20.0, 122 ]

      for daysAgo, systolic in readings do
        let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
        do! page.FillAsync("#Timestamp", now.AddDays(-daysAgo).ToString("yyyy-MM-dd HH:mm"))
        do! page.FillAsync("#Systolic", string systolic)
        do! page.FillAsync("#Diastolic", "76")
        do! page.FillAsync("#HeartRate", "62")
        do! page.ClickAsync("form[action='/readings'] button[type=submit]")
        do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")

      // Starts collapsed; open it so its rows are visible/queryable.
      do! page.ClickAsync(".recent-readings summary")

      let visibleRows () =
        page.Locator(".recent-readings-table tbody tr:visible").AllTextContentsAsync()

      let! initialRows = visibleRows ()
      Assert.Equal(2, initialRows.Count)

      let! _ = page.ClickAsync("button:text('Last 7 days')")
      do! Assertions.Expect(page.Locator(".recent-readings-table tbody tr:visible")).ToHaveCountAsync(1)

      let! narrowedRows = visibleRows ()
      Assert.Contains("111", narrowedRows[0])
      Assert.DoesNotContain("122", narrowedRows[0])

      // Double-click resets to the initial 30-day range (recentXAxis sets an explicit range, not autorange) — both readings fall inside it.
      do! page.DblClickAsync(".chart .js-plotly-plot")
      do! Assertions.Expect(page.Locator(".recent-readings-table tbody tr:visible")).ToHaveCountAsync(2)

      let! resetRows = visibleRows ()
      Assert.Equal(2, resetRows.Count)
    }
