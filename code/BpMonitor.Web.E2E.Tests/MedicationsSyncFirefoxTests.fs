module BpMonitor.Web.E2E.MedicationsSyncFirefoxTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// A collapsed (zero-width) medications timeline makes hoverAt's axis math produce a
/// non-finite pixel; Firefox throws on that MouseEventInit field where Chromium doesn't.
type MedicationsSyncFirefoxTests(fixture: FirefoxWebAppFixture) =
  interface IClassFixture<FirefoxWebAppFixture>

  [<Fact>]
  member _.``hovering the BP chart with the medications timeline collapsed raises no page error``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      let pageErrors = List<string>()
      page.add_PageError (fun _ msg -> pageErrors.Add(msg))

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      // Configure a medication so the (collapsed-by-default) timeline panel renders.
      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")
      do! page.FillAsync("#MedicationName", "Lisinopril")
      do! page.FillAsync("#MedicationStartDate", "01.01.2026")
      do! page.ClickAsync("form[action='/medications'] button[type=submit]")
      let! _ = page.WaitForSelectorAsync("text=Lisinopril")

      // Add a reading now, so it falls inside the default 30-day focus window.
      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
      do! page.FillAsync("#Systolic", "118")
      do! page.FillAsync("#Diastolic", "76")
      do! page.FillAsync("#HeartRate", "62")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      // Fresh /recent load — the medications timeline panel starts collapsed.
      let! _ = page.GotoAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! page.WaitForTimeoutAsync(500.0f)

      let cell = page.Locator("css=.value-strip tr:first-child td[data-x]").First
      let! x = cell.GetAttributeAsync("data-x")
      let! box = cell.BoundingBoxAsync()

      do! page.Mouse.MoveAsync(float32 box.X + float32 box.Width / 2.0f, float32 box.Y + float32 box.Height / 2.0f)

      // Waits for the main chart's own hover to land (unaffected by the collapsed
      // timeline's guard, which only skips the mirrored dispatch onto that chart).
      let! _ =
        page.WaitForFunctionAsync(
          $"""() => document.querySelector('.value-strip td.scrubbed[data-x="{x}"]') !== null"""
        )

      Assert.Empty(pageErrors)
    }
