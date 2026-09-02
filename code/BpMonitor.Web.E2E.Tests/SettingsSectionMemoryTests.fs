module BpMonitor.Web.E2E.SettingsSectionMemoryTests

open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// details-memory.js persists open/closed state per data-persist-key across reloads.
type SettingsSectionMemoryTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``collapsing the goal-range section stays collapsed after a reload``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")

      let details =
        page.Locator(".settings-section[data-persist-key='settings-goal-range']")

      do! details.Locator("summary").First.ClickAsync()

      let! _ = page.ReloadAsync()

      let! isOpen = details.GetAttributeAsync("open")
      Assert.Null(isOpen)
    }
