module BpMonitor.Web.E2E.RecentScrubberFirefoxTests

open System
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// Back-to-back synthetic mousemove dispatches with no intervening mouseout made
/// Plotly's hover throttle skip every other column's scrubber box.
type RecentScrubberFirefoxTests(fixture: FirefoxWebAppFixture) =
  interface IClassFixture<FirefoxWebAppFixture>

  [<Fact>]
  member _.``hovering every value-strip column lights up its scrubber box``() : Task =
    task {
      let! page =
        fixture.Browser.NewPageAsync(BrowserNewPageOptions(ViewportSize = ViewportSize(Width = 1280, Height = 800)))

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      // A configured medication renders the (collapsed-by-default) timeline panel,
      // which shares the same hover-dispatch code path.
      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")
      do! page.FillAsync("#MedicationName", "Lisinopril")
      do! page.FillAsync("#MedicationStartDate", "01.01.2026")
      do! page.ClickAsync("form[action='/medications'] button[type=submit]")
      let! _ = page.WaitForSelectorAsync("text=Lisinopril")

      let now = DateTime.Now

      for i in 0..13 do
        let ts = now.AddHours(-float i * 8.0).ToString("yyyy-MM-dd HH:mm")
        let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
        do! page.FillAsync("#Timestamp", ts)
        do! page.FillAsync("#Systolic", string (110 + i))
        do! page.FillAsync("#Diastolic", string (70 + i))
        do! page.FillAsync("#HeartRate", "60")
        do! page.ClickAsync("form[action='/readings'] button[type=submit]")
        do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! page.WaitForTimeoutAsync(500.0f)

      let! xs =
        page.EvalOnSelectorAllAsync<string[]>(
          ".value-strip tr:first-child td[data-x]",
          "els => els.map(e => e.dataset.x)"
        )

      Assert.NotEmpty(xs)

      for x in xs do
        let! box = page.Locator($"""css=.value-strip tr:first-child td[data-x="{x}"]""").BoundingBoxAsync()

        do! page.Mouse.MoveAsync(float32 box.X + float32 box.Width / 2.0f, float32 box.Y + float32 box.Height / 2.0f)

        let! _ =
          page.WaitForFunctionAsync(
            $"""() => document.querySelector('.value-strip td.scrubbed[data-x="{x}"]') !== null"""
          )

        let! scrubbedXs =
          page.EvalOnSelectorAllAsync<string[]>(".value-strip td.scrubbed", "els => els.map(e => e.dataset.x)")

        Assert.Contains(x, scrubbedXs)
    }
