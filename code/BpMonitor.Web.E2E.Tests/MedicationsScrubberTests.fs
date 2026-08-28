module BpMonitor.Web.E2E.MedicationsScrubberTests

open System
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// A medication bar's fills-hover event fires once on entry, not on every move — the scrubber
/// must keep tracking the cursor across the bar instead of freezing at the entry point.
type MedicationsScrubberTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``moving across a medication bar keeps boxing the matching value-strip column``() : Task =
    task {
      let! page =
        fixture.Browser.NewPageAsync(BrowserNewPageOptions(ViewportSize = ViewportSize(Width = 1280, Height = 800)))

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      // A medication spanning "now" so its bar covers both readings' x positions below.
      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")
      do! page.FillAsync("#MedicationName", "Lisinopril")
      do! page.FillAsync("#MedicationStartDate", "01.01.2026")
      do! page.ClickAsync("form[action='/medications'] button[type=submit]")
      let! _ = page.WaitForSelectorAsync("text=Lisinopril")

      let now = DateTime.Now

      for hoursAgo in [ 4.0; 0.0 ] do
        let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
        do! page.FillAsync("#Timestamp", now.AddHours(-hoursAgo).ToString("yyyy-MM-dd HH:mm"))
        do! page.FillAsync("#Systolic", "118")
        do! page.FillAsync("#Diastolic", "76")
        do! page.FillAsync("#HeartRate", "62")
        do! page.ClickAsync("form[action='/readings'] button[type=submit]")
        do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")

      // The timeline panel starts collapsed — open it so Plotly lays it out at real width.
      do! page.ClickAsync(".medications-timeline summary")
      let! _ = page.WaitForSelectorAsync(".medications-chart .plot-container")
      do! page.WaitForTimeoutAsync(300.0f)

      let! xs = page.Locator(".value-strip tr:first-child td[data-x]").AllTextContentsAsync()
      Assert.True(xs.Count >= 2)
      let! firstX = page.Locator(".value-strip tr:first-child td[data-x]").First.GetAttributeAsync("data-x")
      let! secondX = page.Locator(".value-strip tr:first-child td[data-x]").Nth(1).GetAttributeAsync("data-x")

      // Mirrors medications-sync.js's own d2l/l2p conversion to find an x value's pixel.
      let pixelFor (x: string) =
        page.EvalOnSelectorAsync<float[]>(
          ".medications-chart .js-plotly-plot",
          "(d, x) => { const xa = d._fullLayout.xaxis; const ya = d._fullLayout.yaxis; \
           const rect = d.querySelector('.draglayer .xy > rect').getBoundingClientRect(); \
           return [rect.left + xa.l2p(xa.d2l(x)), rect.top + ya.l2p(0)]; }",
          x
        )

      let scrubbedXs () =
        page.EvalOnSelectorAllAsync<string[]>(".value-strip td.scrubbed", "els => els.map(e => e.dataset.x)")

      let! firstPoint = pixelFor firstX
      do! page.Mouse.MoveAsync(float32 firstPoint[0], float32 firstPoint[1])
      do! page.WaitForTimeoutAsync(300.0f)
      let! scrubbedAfterFirst = scrubbedXs ()
      Assert.Contains(firstX, scrubbedAfterFirst)

      // Move within the same bar (no leave/re-enter) — the entry-only plotly_hover event
      // must not be the only thing driving this, or the scrubber freezes on `firstX`.
      let! secondPoint = pixelFor secondX
      do! page.Mouse.MoveAsync(float32 secondPoint[0], float32 secondPoint[1])
      do! page.WaitForTimeoutAsync(300.0f)
      let! scrubbedAfterSecond = scrubbedXs ()
      Assert.Contains(secondX, scrubbedAfterSecond)
      Assert.DoesNotContain(firstX, scrubbedAfterSecond)
    }

/// Past the timeline's draglayer edge (e.g. the row-label margin), p2d used to extrapolate
/// past the visible range and send the BP chart's mirrored spike off-canvas.
type MedicationsScrubberEdgeTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``drifting past the timeline's draglayer edge while hovering doesn't move the scrubber``() : Task =
    task {
      let! page =
        fixture.Browser.NewPageAsync(BrowserNewPageOptions(ViewportSize = ViewportSize(Width = 1280, Height = 800)))

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")
      do! page.FillAsync("#MedicationName", "Lisinopril")
      do! page.FillAsync("#MedicationStartDate", "01.01.2026")
      do! page.ClickAsync("form[action='/medications'] button[type=submit]")
      let! _ = page.WaitForSelectorAsync("text=Lisinopril")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
      do! page.FillAsync("#Systolic", "118")
      do! page.FillAsync("#Diastolic", "76")
      do! page.FillAsync("#HeartRate", "62")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! page.ClickAsync(".medications-timeline summary")
      let! _ = page.WaitForSelectorAsync(".medications-chart .plot-container")
      do! page.WaitForTimeoutAsync(300.0f)

      let! x = page.Locator(".value-strip tr:first-child td[data-x]").First.GetAttributeAsync("data-x")

      let! point =
        page.EvalOnSelectorAsync<float[]>(
          ".medications-chart .js-plotly-plot",
          "(d, x) => { const xa = d._fullLayout.xaxis; const ya = d._fullLayout.yaxis; \
           const rect = d.querySelector('.draglayer .xy > rect').getBoundingClientRect(); \
           return [rect.left + xa.l2p(xa.d2l(x)), rect.top + ya.l2p(0)]; }",
          x
        )

      do! page.Mouse.MoveAsync(float32 point[0], float32 point[1])
      do! page.WaitForTimeoutAsync(300.0f)

      let scrubbedXs () =
        page.EvalOnSelectorAllAsync<string[]>(".value-strip td.scrubbed", "els => els.map(e => e.dataset.x)")

      let! scrubbedBefore = scrubbedXs ()
      Assert.Contains(x, scrubbedBefore)

      // Dispatch directly on the plot div (not the real cursor, and not on the draglayer
      // itself) so Plotly's own hover state is untouched — isolates our own listener.
      let! _ =
        page.EvalOnSelectorAsync<obj>(
          ".medications-chart .js-plotly-plot",
          "d => { const rect = d.querySelector('.draglayer .xy > rect').getBoundingClientRect(); \
           d.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, clientX: rect.left - 20, clientY: rect.top + 5 })); }"
        )

      do! page.WaitForTimeoutAsync(300.0f)

      let! scrubbedAfter = scrubbedXs ()
      Assert.Equal<string[]>(scrubbedBefore, scrubbedAfter)
    }

/// SpikeSnap.Data snaps to the nearest reading across the BP chart's full loaded data, not
/// just its visible 30-day window — a sparse spot near the edge can snap off-canvas.
type MedicationsScrubberOffscreenSnapTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``hovering a sparse spot near the window edge never puts the scrubber off-canvas``() : Task =
    task {
      let! page =
        fixture.Browser.NewPageAsync(BrowserNewPageOptions(ViewportSize = ViewportSize(Width = 1280, Height = 800)))

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      let now = DateTime.Now

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")
      do! page.FillAsync("#MedicationName", "HCTZ")
      do! page.FillAsync("#MedicationStartDate", now.AddDays(-300.0).ToString("dd.MM.yyyy"))
      do! page.ClickAsync("form[action='/medications'] button[type=submit]")
      let! _ = page.WaitForSelectorAsync("text=HCTZ")

      // One reading well outside the 30-day focus window, one well inside — a wide gap
      // spanning the window's left edge, with no reading anywhere near that boundary.
      for daysAgo in [ 35.0; 3.0 ] do
        let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
        do! page.FillAsync("#Timestamp", now.AddDays(-daysAgo).ToString("yyyy-MM-dd HH:mm"))
        do! page.FillAsync("#Systolic", "118")
        do! page.FillAsync("#Diastolic", "76")
        do! page.FillAsync("#HeartRate", "62")
        do! page.ClickAsync("form[action='/readings'] button[type=submit]")
        do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! page.ClickAsync(".medications-timeline summary")
      let! _ = page.WaitForSelectorAsync(".medications-chart .plot-container")
      do! page.WaitForTimeoutAsync(300.0f)

      // The gap between the two readings, closer to the far (outside-window) one.
      let edgeDate = now.AddDays(-29.5).ToString("yyyy-MM-dd HH:mm")

      let! point =
        page.EvalOnSelectorAsync<float[]>(
          ".medications-chart .js-plotly-plot",
          "(d, x) => { const xa = d._fullLayout.xaxis; const ya = d._fullLayout.yaxis; \
           const rect = d.querySelector('.draglayer .xy > rect').getBoundingClientRect(); \
           return [rect.left + xa.l2p(xa.d2l(x)), rect.top + ya.l2p(0)]; }",
          edgeDate
        )

      do! page.Mouse.MoveAsync(float32 point[0], float32 point[1])
      do! page.WaitForTimeoutAsync(300.0f)

      let! spikeAndChartRect =
        page.EvalOnSelectorAsync<float[]>(
          ".chart .js-plotly-plot",
          "d => { const spike = d.querySelector('.spikeline'); \
           const chartRect = d.querySelector('.draglayer .xy > rect').getBoundingClientRect(); \
           if (!spike) return [chartRect.left, chartRect.right, chartRect.left, chartRect.right]; \
           const spikeRect = spike.getBoundingClientRect(); \
           return [chartRect.left, chartRect.right, spikeRect.left, spikeRect.right]; }"
        )

      let chartLeft, chartRight, spikeLeft, spikeRight =
        spikeAndChartRect[0], spikeAndChartRect[1], spikeAndChartRect[2], spikeAndChartRect[3]

      Assert.True(
        spikeRight >= chartLeft && spikeLeft <= chartRight,
        $"spike [{spikeLeft}, {spikeRight}] rendered outside the visible plot [{chartLeft}, {chartRight}]"
      )
    }

/// The timeline's x-axis is FixedRange (Charts.fs medicationsXAxis) — it only ever follows
/// the BP chart's own range, via medications-sync.js's bpPlot.on("plotly_relayout", ...).
type MedicationsScrubberZoomSyncTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``clicking the Last 7 days button also narrows the timeline's x-axis range``() : Task =
    task {
      let! page =
        fixture.Browser.NewPageAsync(BrowserNewPageOptions(ViewportSize = ViewportSize(Width = 1280, Height = 800)))

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")
      do! page.FillAsync("#MedicationName", "Lisinopril")
      do! page.FillAsync("#MedicationStartDate", "01.01.2026")
      do! page.ClickAsync("form[action='/medications'] button[type=submit]")
      let! _ = page.WaitForSelectorAsync("text=Lisinopril")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
      do! page.FillAsync("#Systolic", "118")
      do! page.FillAsync("#Diastolic", "76")
      do! page.FillAsync("#HeartRate", "62")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! page.ClickAsync(".medications-timeline summary")
      let! _ = page.WaitForSelectorAsync(".medications-chart .plot-container")
      do! page.WaitForTimeoutAsync(300.0f)

      let xaxisRange (selector: string) =
        page.EvalOnSelectorAsync<string[]>(selector, "d => d._fullLayout.xaxis.range.map(String)")

      let! _ = page.ClickAsync("button:text('Last 7 days')")
      do! page.WaitForTimeoutAsync(300.0f)

      let! bpRange = xaxisRange ".chart .js-plotly-plot"
      let! timelineRange = xaxisRange ".medications-chart .js-plotly-plot"

      Assert.Equal<string[]>(bpRange, timelineRange)
    }

/// medications-sync.js mirrors the BP chart's own spike onto the timeline too
/// (bpPlot.on("plotly_hover", ...) / "plotly_unhover"), not just timeline→BP.
type MedicationsScrubberBpToTimelineHoverTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``hovering the BP chart mirrors a spike onto the timeline, and unhovering clears it``() : Task =
    task {
      let! page =
        fixture.Browser.NewPageAsync(BrowserNewPageOptions(ViewportSize = ViewportSize(Width = 1280, Height = 800)))

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")
      do! page.FillAsync("#MedicationName", "Lisinopril")
      do! page.FillAsync("#MedicationStartDate", "01.01.2026")
      do! page.ClickAsync("form[action='/medications'] button[type=submit]")
      let! _ = page.WaitForSelectorAsync("text=Lisinopril")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
      do! page.FillAsync("#Systolic", "118")
      do! page.FillAsync("#Diastolic", "76")
      do! page.FillAsync("#HeartRate", "62")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/recent")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! page.ClickAsync(".medications-timeline summary")
      let! _ = page.WaitForSelectorAsync(".medications-chart .plot-container")
      do! page.WaitForTimeoutAsync(300.0f)

      let! x = page.Locator(".value-strip tr:first-child td[data-x]").First.GetAttributeAsync("data-x")

      // BP-chart y=0 (mmHg) is far below the plotted range, unlike the timeline's category
      // axis — use the draglayer's vertical center instead so the real mouse move lands on-chart.
      let pixelFor (selector: string) (x: string) =
        page.EvalOnSelectorAsync<float[]>(
          selector,
          "(d, x) => { const xa = d._fullLayout.xaxis; \
           const rect = d.querySelector('.draglayer .xy > rect').getBoundingClientRect(); \
           return [rect.left + xa.l2p(xa.d2l(x)), rect.top + rect.height / 2]; }",
          x
        )

      let hasTimelineSpike () =
        page.EvalOnSelectorAsync<bool>(".medications-chart .js-plotly-plot", "d => !!d.querySelector('.spikeline')")

      let! bpPoint = pixelFor ".chart .js-plotly-plot" x
      do! page.Mouse.MoveAsync(float32 bpPoint[0], float32 bpPoint[1])
      do! page.WaitForTimeoutAsync(300.0f)

      let! spikeShownWhileHovering = hasTimelineSpike ()
      Assert.True(spikeShownWhileHovering, "expected the timeline to show a mirrored spike while hovering the BP chart")

      // Move off the chart entirely so Plotly emits plotly_unhover.
      do! page.Mouse.MoveAsync(10.0f, 10.0f)
      do! page.WaitForTimeoutAsync(300.0f)

      let! spikeShownAfterUnhover = hasTimelineSpike ()

      Assert.False(
        spikeShownAfterUnhover,
        "expected the mirrored spike to clear once the BP chart is no longer hovered"
      )
    }

/// /history has no value-strip and a collapsed BP chart — axis-sync must work once opened, without scrubbing a value-strip that doesn't exist there.
type MedicationsScrubberHistoryPageTests(fixture: WebAppFixture) =
  interface IClassFixture<WebAppFixture>

  [<Fact>]
  member _.``opening the collapsed BP chart on /history still syncs the timeline's axis, without scrubbing``() : Task =
    task {
      let! page =
        fixture.Browser.NewPageAsync(BrowserNewPageOptions(ViewportSize = ViewportSize(Width = 1280, Height = 800)))

      do! TestAccount.claimAndLogin fixture.BaseUrl page

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/settings")
      do! page.FillAsync("#MedicationName", "Lisinopril")
      do! page.FillAsync("#MedicationStartDate", "01.01.2026")
      do! page.ClickAsync("form[action='/medications'] button[type=submit]")
      let! _ = page.WaitForSelectorAsync("text=Lisinopril")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/add")
      do! page.FillAsync("#Timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
      do! page.FillAsync("#Systolic", "118")
      do! page.FillAsync("#Diastolic", "76")
      do! page.FillAsync("#HeartRate", "62")
      do! page.ClickAsync("form[action='/readings'] button[type=submit]")
      do! page.WaitForURLAsync($"{fixture.BaseUrl}/recent")

      let! _ = page.GotoAsync($"{fixture.BaseUrl}/history")
      let! valueStripCount = page.Locator(".value-strip").CountAsync()
      Assert.Equal(0, valueStripCount)

      // The BP chart's own <details> starts collapsed on /history — open it, exercising
      // medications-sync.js's bpDetails "toggle" resize+resync path.
      do! page.ClickAsync(".chart-toggle")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! page.ClickAsync(".medications-timeline summary")
      let! _ = page.WaitForSelectorAsync(".medications-chart .plot-container")
      do! page.WaitForTimeoutAsync(300.0f)

      let xaxisRange (selector: string) =
        page.EvalOnSelectorAsync<string[]>(selector, "d => d._fullLayout.xaxis.range.map(String)")

      let! bpRange = xaxisRange ".chart .js-plotly-plot"
      let! timelineRange = xaxisRange ".medications-chart .js-plotly-plot"
      Assert.Equal<string[]>(bpRange, timelineRange)

      // Hover a medication bar — with no value-strip on this page, medications-sync.js's
      // `hasValueStrip` guard must skip mirroring the hover onto the BP chart's spike.
      let pixelFor (selector: string) (x: string) =
        page.EvalOnSelectorAsync<float[]>(
          selector,
          "(d, x) => { const xa = d._fullLayout.xaxis; const ya = d._fullLayout.yaxis; \
           const rect = d.querySelector('.draglayer .xy > rect').getBoundingClientRect(); \
           return [rect.left + xa.l2p(xa.d2l(x)), rect.top + ya.l2p(0)]; }",
          x
        )

      let nowLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
      let! point = pixelFor ".medications-chart .js-plotly-plot" nowLocal

      do! page.Mouse.MoveAsync(float32 point[0], float32 point[1])
      do! page.WaitForTimeoutAsync(300.0f)

      let! bpHasSpike = page.EvalOnSelectorAsync<bool>(".chart .js-plotly-plot", "d => !!d.querySelector('.spikeline')")

      Assert.False(bpHasSpike, "expected /history's BP chart to never receive a mirrored spike (no value-strip there)")
    }
