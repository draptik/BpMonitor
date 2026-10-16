module BpMonitor.Web.E2E.ChartThemeTests

open System
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// theme.js's applyChartTheme restyles the Plotly modebar to match the app theme.
type ChartThemeTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``after switching to dark, the modebar's active tool uses the app's accent color``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName fixture.MemberSetupCode page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", DateTime.Now.AddDays(-1.0).ToString("yyyy-MM-dd HH:mm"))
      do! page.FillAsync("#Systolic", "120")
      do! page.FillAsync("#Diastolic", "80")
      do! page.FillAsync("#HeartRate", "60")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! PlotWaits.laidOut page 0

      do! page.EvaluateAsync("() => { if (document.documentElement.dataset.theme !== 'dark') toggleTheme(); }") :> Task
      do! PlotWaits.framesSettled page

      let! colors =
        page.EvaluateAsync<string[]>(
          """() => [
               document.querySelector('.chart .js-plotly-plot')._fullLayout.modebar.activecolor,
               getComputedStyle(document.documentElement).getPropertyValue('--pico-primary').trim()
             ]"""
        )

      Assert.Equal(colors[1], colors[0])
    }
