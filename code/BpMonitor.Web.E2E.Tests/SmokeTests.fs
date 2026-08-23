module BpMonitor.Web.E2E.SmokeTests

open System
open System.Collections.Generic
open System.Globalization
open System.Net.Http
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
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      // Confirm it appears in the history table.
      let! _ = page.GotoAsync($"{fixture.BaseUrl}/history")
      let! tableText = page.Locator("table").TextContentAsync()
      Assert.Contains("118", tableText)
      Assert.Contains("76", tableText)
      Assert.Contains("62", tableText)
    }

/// Verifies that submitting invalid reading values re-renders the form with
/// visible error messages (not silently discarded by htmx's 422 handling).
type ReadingValidationTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``submitting an out-of-range reading shows error messages on the form``() : Task =
    task {
      let! page = fixture.Browser.NewPageAsync()

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", "2026-06-19 08:30")
      do! page.FillAsync("#Systolic", "999")
      do! page.FillAsync("#Diastolic", "80")
      do! page.FillAsync("#HeartRate", "66")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")

      let! _ = page.WaitForSelectorAsync(".errors")

      let! errorText = page.Locator(".errors").TextContentAsync()
      Assert.Contains("out of range", errorText)
    }

/// Verifies HTTP security properties of the running server via raw HttpClient
/// (no browser required — just inspects response headers).
type CookieSecurityTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``auth Set-Cookie always includes HttpOnly attribute``() : Task =
    task {
      use handler = new HttpClientHandler(AllowAutoRedirect = false)
      use client = new HttpClient(handler)

      // POST /login with just a username — the "Me" account starts unclaimed,
      // so the server redirects to the per-member claim page without checking
      // the password field.
      use step1Body =
        new FormUrlEncodedContent([ KeyValuePair("Username", TestAccount.username); KeyValuePair("Password", "") ])

      let! redirectResp = client.PostAsync($"{fixture.BaseUrl}/login", step1Body)
      let claimUrl = Uri(Uri(fixture.BaseUrl), redirectResp.Headers.Location).ToString()

      // Claim the account — SignInAsync fires here and emits the Set-Cookie header.
      use step2Body =
        new FormUrlEncodedContent(
          [ KeyValuePair("Password", TestAccount.password)
            KeyValuePair("PasswordConfirm", TestAccount.password) ]
        )

      let! signInResp = client.PostAsync(claimUrl, step2Body)

      let setCookieHeader =
        signInResp.Headers.GetValues("Set-Cookie") |> String.concat " "

      Assert.Contains("httponly", setCookieHeader.ToLower())
      Assert.Contains("samesite=lax", setCookieHeader.ToLower())
    }

/// Verifies that omitting "remember me" yields a session cookie (no Expires/Max-Age
/// — dies with the browser). A separate type so this gets its own fixture (own
/// process, own DB): the claim is the only login this "Me" account goes through.
type RememberMeUncheckedCookieTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``omitting remember-me yields a session cookie with no Expires``() : Task =
    task {
      use handler = new HttpClientHandler(AllowAutoRedirect = false)
      use client = new HttpClient(handler)

      use step1Body =
        new FormUrlEncodedContent([ KeyValuePair("Username", TestAccount.username); KeyValuePair("Password", "") ])

      let! redirectResp = client.PostAsync($"{fixture.BaseUrl}/login", step1Body)
      let claimUrl = Uri(Uri(fixture.BaseUrl), redirectResp.Headers.Location).ToString()

      // Claim without checking "remember me".
      use step2Body =
        new FormUrlEncodedContent(
          [ KeyValuePair("Password", TestAccount.password)
            KeyValuePair("PasswordConfirm", TestAccount.password) ]
        )

      let! signInResp = client.PostAsync(claimUrl, step2Body)

      // Only the auth cookie is asserted: `bpmonitor_lang` is a language *preference*,
      // not a session token, so it is deliberately persistent regardless of remember-me.
      let authCookie =
        signInResp.Headers.GetValues("Set-Cookie")
        |> Seq.find (fun c -> c.StartsWith(".AspNetCore.Cookies="))
        |> _.ToLower()

      Assert.DoesNotContain("expires=", authCookie)
      Assert.DoesNotContain("max-age=", authCookie)
    }

/// Verifies that checking "remember me" yields a persistent cookie (an Expires
/// attribute) — the whole point of the feature. Own fixture, so the claim (without
/// remember-me) followed by a direct re-login (with remember-me) is the only
/// sequence this "Me" account goes through.
type RememberMeCheckedCookieTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``checking remember-me yields a persistent cookie with an Expires attribute``() : Task =
    task {
      use handler = new HttpClientHandler(AllowAutoRedirect = false)
      use client = new HttpClient(handler)

      // Claim the account first (without remember-me) so this test can then
      // exercise the direct (already-claimed) login path with the checkbox set.
      use claimStep1 =
        new FormUrlEncodedContent([ KeyValuePair("Username", TestAccount.username); KeyValuePair("Password", "") ])

      let! redirectResp = client.PostAsync($"{fixture.BaseUrl}/login", claimStep1)
      let claimUrl = Uri(Uri(fixture.BaseUrl), redirectResp.Headers.Location).ToString()

      use claimStep2 =
        new FormUrlEncodedContent(
          [ KeyValuePair("Password", TestAccount.password)
            KeyValuePair("PasswordConfirm", TestAccount.password) ]
        )

      let! _ = client.PostAsync(claimUrl, claimStep2)

      // Now log in again with "remember me" checked.
      use rememberMeBody =
        new FormUrlEncodedContent(
          [ KeyValuePair("Username", TestAccount.username)
            KeyValuePair("Password", TestAccount.password)
            KeyValuePair("RememberMe", "on") ]
        )

      let! signInResp = client.PostAsync($"{fixture.BaseUrl}/login", rememberMeBody)

      let setCookieHeader =
        signInResp.Headers.GetValues("Set-Cookie") |> String.concat " " |> _.ToLower()

      Assert.Contains("expires=", setCookieHeader)
    }

/// Plotly auto-detects /recent's x-axis as a date type and would otherwise apply
/// its own locale-formatted default ("Aug 3, 2026, 12:45") to the unified hover
/// label, independently of the tick labels — guards the explicit HoverFormat
/// (Charts.fs's recentXAxis) that keeps it on the app's yyyy-MM-dd HH:mm convention.
type RecentChartHoverFormatTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``recent chart hover shows date, time, and day name``() : Task =
    task {
      let! page = fixture.Browser.NewPageAsync()

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      let now = DateTime.Now
      let ts = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
      do! page.FillAsync("#Timestamp", ts)
      do! page.FillAsync("#Systolic", "118")
      do! page.FillAsync("#Diastolic", "76")
      do! page.FillAsync("#HeartRate", "62")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.WaitForSelectorAsync(".chart .plot-container")

      let! rectJson =
        page.EvalOnSelectorAsync<string>(
          ".chart .scatterlayer path.point",
          "el => { const r = el.getBoundingClientRect(); return JSON.stringify({x: r.x + r.width/2, y: r.y + r.height/2}); }"
        )

      let rect = System.Text.Json.JsonDocument.Parse(rectJson).RootElement
      let px = rect.GetProperty("x").GetSingle()
      let py = rect.GetProperty("y").GetSingle()
      do! page.Mouse.MoveAsync(px, py)
      do! page.WaitForTimeoutAsync(500.0f)

      let! headerText = page.Locator(".chart .hoverlayer .axistext text").TextContentAsync()
      let expected = now.ToString("yyyy-MM-dd HH:mm (ddd)", CultureInfo.InvariantCulture)
      Assert.Equal(expected, headerText)
    }
