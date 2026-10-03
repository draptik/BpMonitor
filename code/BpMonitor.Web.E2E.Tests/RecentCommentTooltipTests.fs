module BpMonitor.Web.E2E.RecentCommentTooltipTests

open System
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// recent-scrubber.js's custom comment tooltip, for a German-language member (localized trace name).
type RecentCommentTooltipTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``hovering a comment marker shows the comment verbatim for a German member``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName page

      let comment = "Kopfschmerzen, müde & Kaffee"
      let ts = DateTime.Now.AddDays(-3.0).ToString("yyyy-MM-dd HH:mm")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", ts)
      do! page.FillAsync("#Systolic", "120")
      do! page.FillAsync("#Diastolic", "80")
      do! page.FillAsync("#HeartRate", "60")
      do! page.FillAsync("#Comments", comment)
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")
      let! _ = page.SelectOptionAsync("#Language", "de")
      do! page.ClickAsync("form[action='/settings/language'] button[type=submit]")
      do! page.WaitForLoadStateAsync()

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! PlotWaits.laidOut page 0

      // The marker sits at (reading timestamp, y = 0); same geometry recent-scrubber.js uses.
      let! pos =
        page.EvaluateAsync<float[]>(
          """ts => {
               const g = chartGeometry(document.querySelector('.chart .js-plotly-plot'));
               return [g.br.left + g.xaxis.l2p(g.xaxis.d2l(ts)), g.br.top + g.yaxis.l2p(0)];
             }""",
          ts
        )

      do! page.Mouse.MoveAsync(float32 pos[0], float32 pos[1])

      do! Assertions.Expect(page.Locator(".comment-tooltip > div").First).ToHaveTextAsync(comment)
    }
