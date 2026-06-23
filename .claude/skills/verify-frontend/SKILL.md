---
name: verify-frontend
description: Verify a frontend/UI change by driving a real browser against a real BpMonitor.Web instance, using a throwaway xunit test in BpMonitor.Web.E2E.Tests. Invoke after making any change to wwwroot/, Views, or page-rendering handlers, before reporting the change as complete.
---

# Verify Frontend

Confirm a frontend change actually works by reusing `BpMonitor.Web.E2E.Tests`'
existing infrastructure — not ad-hoc Playwright scripts or screenshot tools.
`WebAppFixture` already boots a real out-of-process `BpMonitor.Web` instance
(fresh temp SQLite file) and launches a real headless Chromium browser; reuse
it instead of reinventing browser automation per session.

## Steps

1. **Check Chromium is installed.** If `mise run test:e2e-setup` hasn't been
   run on this machine, the test will fail to launch the browser — run it
   once if needed.
2. **Add a throwaway `[<Fact>]`** to `code/BpMonitor.Web.E2E.Tests/SmokeTests.fs`
   (or a scratch file in the same project), inside a type that implements
   `IClassFixture<WebAppFixture>`. Reuse `TestAccount.claimAndLogin` to get an
   authenticated session, then navigate to the page the change touched and
   assert on the specific thing that should now be true (text content, an
   element existing, a redirect happening).
3. **Run only that test:**

   ```bash
   cd code
   dotnet test BpMonitor.Web.E2E.Tests --filter "FullyQualifiedName~<unique-name-fragment>"
   ```

   This exercises the same path CI does — real HTTP, real routing, real
   htmx/CSS — so a pass here is a real signal.
4. **For visual/CSS changes** where an assertion alone won't tell the full
   story, launch headed instead of headless for that one run:
   `playwright.Chromium.LaunchAsync(BrowserTypeLaunchOptions(Headless = false))`
   (temporarily edit `WebAppFixture.InitializeAsync`, then revert).
5. **Delete the scratch test** once the change is confirmed. It existed only
   to prove the change works — don't leave it in the permanent suite unless
   it earns a place as real regression coverage (a deliberate decision, not a
   byproduct of verification).

## When NOT to use this

- Backend-only changes with no rendered output — ordinary unit/integration
  tests cover those.
- Changes already covered by an existing `SmokeTests.fs` test that exercises
  the same path — rerun that test instead of writing a new one.
