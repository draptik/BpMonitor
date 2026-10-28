module BpMonitor.Web.E2E.ThemeToggleTests

open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// The theme toggle shows the current theme's icon and offers the other theme in its
/// tooltip, also after an hx-boost navigation or a Back restore swaps in a stale tooltip.
let private showsCurrentThemeAndOffersTheOther (fixture: MemberFixture) : Task =
  task {
    use! traced = fixture.NewTracedPageAsync()
    let page = traced.Page

    do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName fixture.MemberSetupCode page

    let toggle = page.Locator(".topbar .theme-toggle")

    // Retrying assertions: an hx-boost swap and its resync land after the URL changes.
    let expectTheme (title: string) (sunShown: bool) =
      task {
        do! Assertions.Expect(toggle).ToHaveAttributeAsync("title", title)
        do! Assertions.Expect(toggle).ToHaveAttributeAsync("aria-label", title)

        let sun, moon =
          toggle.Locator(".theme-icon-light"), toggle.Locator(".theme-icon-dark")

        let shown, hidden = if sunShown then sun, moon else moon, sun
        do! Assertions.Expect(shown).ToBeVisibleAsync()
        do! Assertions.Expect(hidden).ToBeHiddenAsync()
      }

    do! page.EvaluateAsync("() => { if (document.documentElement.dataset.theme !== 'light') toggleTheme(); }") :> Task
    do! expectTheme "Switch to dark theme" true

    do! toggle.ClickAsync()
    do! expectTheme "Switch to light theme" false

    // Dark now: the swapped-in body carries the server's light-theme tooltip until resynced.
    // Waiting for the new heading keeps the checks off the old body, which already matches.
    let heading = page.Locator("main h1")
    do! page.ClickAsync("nav.sidebar a[href='/history']")
    do! Assertions.Expect(heading).ToHaveTextAsync("History")
    do! expectTheme "Switch to light theme" false

    // Light now: Back restores the landing snapshot, saved while dark, without an afterSwap.
    do! toggle.ClickAsync()
    do! expectTheme "Switch to dark theme" true
    let! _ = page.GoBackAsync()
    do! Assertions.Expect(heading).ToHaveTextAsync("BpMonitor")
    do! expectTheme "Switch to dark theme" true
  }

/// Without JS no data-theme is set and Pico follows the OS preference; the icon must too.
let private followsOsThemeWithoutDataTheme (fixture: MemberFixture) : Task =
  task {
    use! traced = fixture.NewTracedPageAsync()
    let page = traced.Page

    do! page.EmulateMediaAsync(PageEmulateMediaOptions(ColorScheme = ColorScheme.Dark))
    let! _ = page.GotoAsync($"{fixture.BaseUrl}/login")
    do! page.EvaluateAsync("() => document.documentElement.removeAttribute('data-theme')") :> Task

    let toggle = page.Locator(".theme-toggle")
    do! Assertions.Expect(toggle.Locator(".theme-icon-dark")).ToBeVisibleAsync()
    do! Assertions.Expect(toggle.Locator(".theme-icon-light")).ToBeHiddenAsync()
  }

type ThemeToggleTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``theme toggle shows the current theme's icon and a tooltip naming the other theme``() : Task =
    showsCurrentThemeAndOffersTheOther fixture

  [<Fact>]
  member _.``theme toggle follows the OS theme when no theme is set``() : Task = followsOsThemeWithoutDataTheme fixture

/// Firefox variant: the icon swap relies on CSS selectors over :root's data-theme.
type ThemeToggleFirefoxTests(fixture: FirefoxFixture) =
  interface IClassFixture<FirefoxFixture>

  [<Fact>]
  member _.``theme toggle shows the current theme's icon and a tooltip naming the other theme (Firefox)``() : Task =
    showsCurrentThemeAndOffersTheOther fixture

  [<Fact>]
  member _.``theme toggle follows the OS theme when no theme is set (Firefox)``() : Task =
    followsOsThemeWithoutDataTheme fixture
