module BpMonitor.Web.E2E.MobileOverflowTests

open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// Opens `path` on a phone-sized page after `login`, runs `prepare`, and returns (scrollWidth, clientWidth).
let private phoneWidthsAsync
  (fixture: MemberFixture)
  (path: string)
  (login: IPage -> Task)
  (prepare: IPage -> Task)
  : Task<int * int> =
  task {
    use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 412, Height = 800))
    let page = traced.Page
    do! login page
    let! _ = page.GotoAsync($"{fixture.BaseUrl}{path}")
    do! prepare page

    let! scrollWidth =
      page.EvaluateAsync<int>("() => document.documentElement.scrollWidth")

    let! clientWidth =
      page.EvaluateAsync<int>("() => document.documentElement.clientWidth")

    return scrollWidth, clientWidth
  }

let private ownMember (fixture: MemberFixture) =
  TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName fixture.MemberSetupCode

let private nothing (_: IPage) = Task.CompletedTask

/// A page wider than the phone screen stretches the fixed topbar off-screen on mobile browsers.
type MobileOverflowTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

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
  member _.``settings page does not scroll horizontally on a phone``() : Task =
    task {
      let! scrollWidth, clientWidth =
        phoneWidthsAsync fixture "/settings" (ownMember fixture) nothing

      Assert.Equal(clientWidth, scrollWidth)
    }

  [<Fact>]
  member this.``members page does not scroll horizontally on a phone``() : Task =
    task {
      let! scrollWidth, clientWidth =
        phoneWidthsAsync fixture "/members" this.LoginAsAdmin nothing

      Assert.Equal(clientWidth, scrollWidth)
    }

/// Own class: each claims the class's member, which only works once.
type RecentMobileOverflowTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``recent view with readings expanded does not scroll horizontally on a phone``() : Task =
    task {
      let expandReadings (page: IPage) =
        page.ClickAsync("details.recent-readings > summary")

      let! scrollWidth, clientWidth =
        phoneWidthsAsync fixture "/recent" (ownMember fixture) expandReadings

      Assert.Equal(clientWidth, scrollWidth)
    }
