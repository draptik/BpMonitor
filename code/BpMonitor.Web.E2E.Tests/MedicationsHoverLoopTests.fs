module BpMonitor.Web.E2E.MedicationsHoverLoopTests

open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// BP chart → timeline hover mirroring must not bounce back onto the BP chart; covers
/// medications-sync.js's `syncing` guard (removing it makes this fail).
type MedicationsHoverLoopTests(fixture: FirefoxFixture) =
  interface IClassFixture<FirefoxFixture>

  [<Fact>]
  member _.``hovering the BP chart never dispatches a synthetic hover back onto it``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName fixture.MemberSetupCode page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")
      do! page.FillAsync("#MedicationName", "Lisinopril")
      do! page.FillAsync("#MedicationStartDate", "01.01.2026")
      do! page.ClickAsync("form[action='/medications'] button[type=submit]")
      let! _ = page.WaitForSelectorAsync("text=Lisinopril")

      let now = ReadingTimes.insideWindow ()

      for hoursAgo in [ 72.0; 48.0; 24.0; 0.0 ] do
        let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
        do! page.FillAsync("#Timestamp", now.AddHours(-hoursAgo).ToString("yyyy-MM-dd HH:mm"))
        do! page.FillAsync("#Systolic", "118")
        do! page.FillAsync("#Diastolic", "76")
        do! page.FillAsync("#HeartRate", "62")
        do! page.ClickAsync("form[action='/readings'] button[type=submit]")
        do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! page.ClickAsync(".medications-timeline summary")
      let! _ = page.WaitForSelectorAsync(".medications-chart .plot-container")
      do! PlotWaits.laidOut page 1

      // Counts synthetic (untrusted) mousemoves landing on the BP chart; the user's real ones are trusted.
      let! (bounds: float[]) =
        page.EvaluateAsync<float[]>(
          "() => { const d = document.querySelector('.chart .js-plotly-plot');"
          + " const rect = d.querySelector('.draglayer .xy > rect'); window.__bounced = 0;"
          + " rect.addEventListener('mousemove', e => { if (!e.isTrusted) window.__bounced++; });"
          + " const r = rect.getBoundingClientRect(); return [r.left, r.right, r.top + r.height / 2]; }"
        )

      let left, right, midY = bounds[0], bounds[1], bounds[2]

      for i in 0..40 do
        let x = left + (right - left) * float i / 40.0
        do! page.Mouse.MoveAsync(float32 x, float32 midY)
        do! Task.Delay 16

      do! Task.Delay 300
      let! bounced = page.EvaluateAsync<int>("() => window.__bounced")
      Assert.Equal(0, bounced)
    }
