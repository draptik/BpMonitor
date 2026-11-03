module BpMonitor.Web.E2E.MobileOverflowTests

open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// A page wider than the phone screen stretches the fixed topbar off-screen on mobile browsers.
type MobileOverflowTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  member private _.PageWidthsAsync(path: string, login: IPage -> Task) : Task<int * int> =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 412, Height = 800))
      let page = traced.Page
      do! login page
      let! _ = page.GotoAsync($"{fixture.BaseUrl}{path}")

      let! scrollWidth =
        page.EvaluateAsync<int>("() => document.documentElement.scrollWidth")

      let! clientWidth =
        page.EvaluateAsync<int>("() => document.documentElement.clientWidth")

      return scrollWidth, clientWidth
    }

  /// The seeded admin "Me" (claimed by AppFixture), since /members is admin-only.
  member private _.LoginAsAdmin(page: IPage) : Task =
    task {
      let! _ = page.GotoAsync($"{fixture.BaseUrl}/login")
      do! page.FillAsync("#Username", "Me")
      do! page.FillAsync("#Password", TestAccount.password)
      do! page.ClickAsync("button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/")
    }

  [<Fact>]
  member this.``settings page does not scroll horizontally on a phone``() : Task =
    task {
      let! scrollWidth, clientWidth =
        this.PageWidthsAsync(
          "/settings",
          TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName fixture.MemberSetupCode
        )

      Assert.Equal(clientWidth, scrollWidth)
    }

  [<Fact>]
  member this.``members page does not scroll horizontally on a phone``() : Task =
    task {
      let! scrollWidth, clientWidth = this.PageWidthsAsync("/members", this.LoginAsAdmin)
      Assert.Equal(clientWidth, scrollWidth)
    }
