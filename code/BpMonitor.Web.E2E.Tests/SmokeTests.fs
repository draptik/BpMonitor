module BpMonitor.Web.E2E.SmokeTests

open System
open System.Collections.Generic
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
    }
