module BpMonitor.Web.E2E.SmokeTests

open System.Threading.Tasks
open BpMonitor.Web.E2E
open Xunit

/// End-to-end smoke test: claim the default account, add a reading, and
/// confirm it shows up in the history table. Runs against a real
/// out-of-process BpMonitor.Web instance with a real browser.
type LoginAddHistoryTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``login, add a reading, and see it in history``() : Task =
    task {
      let! page = fixture.Browser.NewPageAsync()

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      // Add a reading.
      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", "2026-06-19 08:30")
      do! page.FillAsync("#Systolic", "118")
      do! page.FillAsync("#Diastolic", "76")
      do! page.FillAsync("#HeartRate", "62")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/history")

      // Confirm it appears in the history table.
      let! tableText = page.Locator("table").TextContentAsync()
      Assert.Contains("118", tableText)
      Assert.Contains("76", tableText)
      Assert.Contains("62", tableText)
    }
