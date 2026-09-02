module BpMonitor.Web.E2E.MedicationsScrubberTests

open System
open System.Threading.Tasks
open BpMonitor.Web.E2E
open Microsoft.Playwright
open Xunit

/// A medication bar's fills-hover event fires once on entry, not on every move — the scrubber
/// must keep tracking the cursor across the bar instead of freezing at the entry point.
type MedicationsScrubberTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``moving across a medication bar keeps boxing the matching value-strip column``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName page

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
      do! PlotWaits.laidOut page 1

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

      let! firstPoint = pixelFor firstX
      do! page.Mouse.MoveAsync(float32 firstPoint[0], float32 firstPoint[1])
      let! scrubbedAfterFirst = PlotWaits.scrubbedContainsStable page firstX
      Assert.Contains(firstX, scrubbedAfterFirst)

      // Move within the same bar (no leave/re-enter) — the entry-only plotly_hover event
      // must not be the only thing driving this, or the scrubber freezes on `firstX`.
      let! secondPoint = pixelFor secondX
      do! page.Mouse.MoveAsync(float32 secondPoint[0], float32 secondPoint[1])
      let! scrubbedAfterSecond = PlotWaits.scrubbedTransitionedTo page secondX firstX
      Assert.Contains(secondX, scrubbedAfterSecond)
      Assert.DoesNotContain(firstX, scrubbedAfterSecond)
    }

/// Past the timeline's draglayer edge (e.g. the row-label margin), p2d used to extrapolate
/// past the visible range and send the BP chart's mirrored spike off-canvas.
type MedicationsScrubberEdgeTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``drifting past the timeline's draglayer edge while hovering doesn't move the scrubber``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName page

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
      do! PlotWaits.laidOut page 1

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
      let! scrubbedBefore = PlotWaits.scrubbedContainsStable page x
      Assert.Contains(x, scrubbedBefore)

      // Dispatch directly on the plot div (not the real cursor, and not on the draglayer
      // itself) so Plotly's own hover state is untouched — isolates our own listener.
      let! _ =
        page.EvalOnSelectorAsync<obj>(
          ".medications-chart .js-plotly-plot",
          "d => { const rect = d.querySelector('.draglayer .xy > rect').getBoundingClientRect(); \
           d.dispatchEvent(new MouseEvent('mousemove', { bubbles: true, clientX: rect.left - 20, clientY: rect.top + 5 })); }"
        )

      // Nothing should happen here — read a settled snapshot rather than guessing a sleep length.
      let! scrubbedAfter = PlotWaits.stableScrubbed page
      Assert.Equal<string[]>(scrubbedBefore, scrubbedAfter)
    }

/// SpikeSnap.Data snaps to the nearest reading across the BP chart's full loaded data, not
/// just its visible 30-day window — a sparse spot near the edge can snap off-canvas.
type MedicationsScrubberOffscreenSnapTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``hovering a sparse spot near the window edge never puts the scrubber off-canvas``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName page

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
      do! PlotWaits.laidOut page 1

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

      // The assertion below already tolerates a missing spike (falls back to the
      // chart's own rect), so this only needs to settle, not require one to appear.
      do! PlotWaits.framesSettled page

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
type MedicationsScrubberZoomSyncTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``clicking the Last 7 days button also narrows the timeline's x-axis range``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName page

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
      do! PlotWaits.laidOut page 1

      let xaxisRange (selector: string) =
        page.EvalOnSelectorAsync<string[]>(selector, "d => d._fullLayout.xaxis.range.map(String)")

      let! _ = page.ClickAsync("button:text('Last 7 days')")

      // recent-zoom.js's Plotly.relayout narrows the range asynchronously — wait for
      // the actual span to shrink to ~7 days instead of guessing how long that takes.
      let! _ =
        page.WaitForFunctionAsync(
          "() => { const r = document.querySelector('.chart .js-plotly-plot')._fullLayout.xaxis.range; \
           return (new Date(r[1]) - new Date(r[0])) <= 8 * 24 * 3600 * 1000; }"
        )

      let! bpRange = xaxisRange ".chart .js-plotly-plot"
      let! timelineRange = xaxisRange ".medications-chart .js-plotly-plot"

      Assert.Equal<string[]>(bpRange, timelineRange)
    }

/// medications-sync.js mirrors the BP chart's own spike onto the timeline too
/// (bpPlot.on("plotly_hover", ...) / "plotly_unhover"), not just timeline→BP.
type MedicationsScrubberBpToTimelineHoverTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``hovering the BP chart mirrors a spike onto the timeline, and unhovering clears it``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName page

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
      do! PlotWaits.laidOut page 1

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

      let! _ =
        page.WaitForFunctionAsync(
          "() => !!document.querySelector('.medications-chart .js-plotly-plot')?.querySelector('.spikeline')"
        )

      let! spikeShownWhileHovering = hasTimelineSpike ()
      Assert.True(spikeShownWhileHovering, "expected the timeline to show a mirrored spike while hovering the BP chart")

      // Move off the chart entirely so Plotly emits plotly_unhover.
      do! page.Mouse.MoveAsync(10.0f, 10.0f)

      let! _ =
        page.WaitForFunctionAsync(
          "() => !document.querySelector('.medications-chart .js-plotly-plot')?.querySelector('.spikeline')"
        )

      let! spikeShownAfterUnhover = hasTimelineSpike ()

      Assert.False(
        spikeShownAfterUnhover,
        "expected the mirrored spike to clear once the BP chart is no longer hovered"
      )
    }

/// /history has no value-strip and a collapsed BP chart — axis-sync must work once opened, without scrubbing a value-strip that doesn't exist there.
type MedicationsScrubberHistoryPageTests(fixture: ChromiumFixture) =
  interface IClassFixture<ChromiumFixture>

  [<Fact>]
  member _.``opening the collapsed BP chart on /history still syncs the timeline's axis, without scrubbing``() : Task =
    task {
      use! traced = fixture.NewTracedPageAsync(ViewportSize(Width = 1280, Height = 800))
      let page = traced.Page

      do! TestAccount.claimAndLogin fixture.BaseUrl fixture.MemberName page

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
      do! page.ClickAsync("details.collapsible:not(.medications-timeline) > summary")
      let! _ = page.WaitForSelectorAsync(".chart .plot-container")
      do! page.ClickAsync(".medications-timeline summary")
      let! _ = page.WaitForSelectorAsync(".medications-chart .plot-container")
      do! PlotWaits.laidOut page 1

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

      // Nothing should happen here (no value-strip means no mirrored spike), so
      // there's no positive condition to poll for — flush the scheduled update instead.
      do! PlotWaits.framesSettled page

      let! bpHasSpike = page.EvalOnSelectorAsync<bool>(".chart .js-plotly-plot", "d => !!d.querySelector('.spikeline')")

      Assert.False(bpHasSpike, "expected /history's BP chart to never receive a mirrored spike (no value-strip there)")
    }
