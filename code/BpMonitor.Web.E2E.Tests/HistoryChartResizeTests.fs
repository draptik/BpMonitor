module BpMonitor.Web.E2E.HistoryChartResizeTests

open System
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// /history's BP chart renders inside a collapsed <details>, so Plotly lays it out
/// while hidden; opening it must refit the chart even without a Medications Timeline.
/// Firefox only: Chromium still lays out a closed <details>'s content, hiding the bug.
type HistoryChartResizeTests(fixture: FirefoxFixture) =
  interface IClassFixture<FirefoxFixture>

  [<Fact>]
  member _.``opening the collapsed history chart fits it to its container without medications``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 400, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName fixture.MemberSetupCode page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", DateTime.Now.AddDays(-1.0).ToString("yyyy-MM-dd HH:mm"))
      do! page.FillAsync("#Systolic", "120")
      do! page.FillAsync("#Diastolic", "76")
      do! page.FillAsync("#HeartRate", "62")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/history")
      let! timelineCount = page.Locator(".medications-timeline").CountAsync()
      Assert.Equal(0, timelineCount)

      do! page.ClickAsync("details[data-persist-key='history-chart'] > summary")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")

      let plotFitsContainer =
        "() => { const c = document.querySelector('.chart'); const svg = c.querySelector('.main-svg');"
        + " return Math.abs(svg.getBoundingClientRect().width - c.clientWidth) <= 1; }"

      do! page.WaitForFunctionAsync(plotFitsContainer, null, PageWaitForFunctionOptions(Timeout = 3000.0f)) :> Task
    }
