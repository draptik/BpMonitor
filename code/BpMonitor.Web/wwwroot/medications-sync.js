// Links the Medications Timeline chart (Charts.fs BpChart.toHtmlMedications) to the BP
// chart above it (Wegier et al. 2021 Fig. 5): keeps their x-axes in sync on pan/zoom, and
// mirrors the scrubber (green spike line) between them in both directions. Waits for both
// plots via whenPlotReady (plot-ready.js) — index 0 is always the BP chart, index 1 the
// timeline, per the DOM order ReadingViews.fs renders them in.
//
// /history has no scrubber (Charts.fs medicationsXAxis's spike is BP-chart-driven only via
// /recent's `showScrubber`), so only the axis-sync half of this file does anything there;
// the hover-mirroring handlers below are harmless no-ops without a spike to move.
function setupMedicationsSync() {
  const timelineDetails = /** @type {HTMLDetailsElement | null} */ (
    document.querySelector(".medications-timeline")
  );
  if (!timelineDetails) return;

  /** @param {PlotlyChartElement} d */
  function chartGeometry(d) {
    const xaxis = d._fullLayout?.xaxis;
    const yaxis = d._fullLayout?.yaxis;
    if (!xaxis?.d2l || !xaxis?.l2p) return null;
    const dragRect = d.querySelector(".draglayer .xy > rect");
    if (!dragRect) return null;
    return { xaxis, yaxis, dragRect, br: dragRect.getBoundingClientRect() };
  }

  /**
   * @param {PlotlyChartElement} target
   * @param {string} x
   */
  function hoverAt(target, x) {
    const geo = chartGeometry(target);
    if (!geo) return;
    const { xaxis, dragRect, br } = geo;
    const xPx = xaxis.l2p(xaxis.d2l(x));
    const yPx = geo.yaxis?.l2p?.(0) ?? br.height / 2;
    dragRect.dispatchEvent(
      new MouseEvent("mousemove", {
        bubbles: true,
        cancelable: true,
        clientX: br.left + xPx,
        clientY: br.top + yPx,
      }),
    );
  }

  /** @param {PlotlyChartElement} target */
  function unhover(target) {
    const dragRect = chartGeometry(target)?.dragRect;
    if (dragRect) dragRect.dispatchEvent(new MouseEvent("mouseout", { bubbles: true }));
  }

  whenPlotReady((bpPlot) => {
    whenPlotReady((timelinePlot) => {
      // Setup runs on every htmx:afterSettle; skip plots already wired so a settle
      // that doesn't swap the chart can't stack duplicate handlers.
      if (timelinePlot.dataset.medicationsSyncBound) return;
      timelinePlot.dataset.medicationsSyncBound = "1";

      // Charts.fs configures both charts with the same Margin.Left/Right (48/16), but
      // the BP chart's y-axis title ("blood pressure [mmHg]") makes Plotly auto-expand
      // its *actual* rendered margin beyond that (measured, not the requested value —
      // `_fullLayout.margin.l` still reports 48; `_fullLayout._size.l` has the real
      // number). The timeline has no y-axis title, so it never gets that expansion and
      // its plot area starts several pixels to the left of the BP chart's — copy the
      // BP chart's real margin onto the timeline so their x-axes align pixel-for-pixel.
      const bpSize = bpPlot._fullLayout._size;
      Plotly.relayout(timelinePlot, { "margin.l": bpSize.l, "margin.r": bpSize.r });

      // BP chart → timeline: mirror the spike position.
      bpPlot.on("plotly_hover", (e) => {
        hoverAt(timelinePlot, e.points[0].x);
      });
      bpPlot.on("plotly_unhover", () => {
        unhover(timelinePlot);
      });

      // Timeline → BP chart: hovering a medication bar moves the BP chart's spike (and,
      // via recent-scrubber.js's existing plotly_hover listener, the value strip's box).
      timelinePlot.on("plotly_hover", (e) => {
        hoverAt(bpPlot, e.points[0].x);
      });
      timelinePlot.on("plotly_unhover", () => {
        unhover(bpPlot);
      });

      // Pan/zoom (including the Last 7/30 days buttons, via recent-zoom.js's
      // Plotly.relayout call) — the timeline's x-axis is FixedRange (Charts.fs
      // medicationsXAxis), so it only ever follows the BP chart, never the reverse.
      bpPlot.on("plotly_relayout", (e) => {
        let lo = e["xaxis.range[0]"];
        let hi = e["xaxis.range[1]"];

        if (lo === undefined && Array.isArray(e["xaxis.range"])) {
          lo = e["xaxis.range"][0];
          hi = e["xaxis.range"][1];
        }

        if (lo === undefined || hi === undefined) return;

        Plotly.relayout(timelinePlot, { "xaxis.range": [lo, hi] });
      });

      // Plotly renders at zero width inside a closed <details>; resize the first time
      // it's opened so the chart actually appears instead of collapsing to nothing.
      timelineDetails.addEventListener("toggle", () => {
        if (timelineDetails.open) Plotly.Plots.resize(timelinePlot);
      });

      // details-memory.js may have already restored an open state (from localStorage)
      // before this async setup finished — that toggle fired before the listener above
      // existed to catch it, so resize once now to cover that case.
      if (timelineDetails.open) Plotly.Plots.resize(timelinePlot);
    }, 1);
  }, 0);
}

document.addEventListener("DOMContentLoaded", setupMedicationsSync);
document.addEventListener("htmx:afterSettle", setupMedicationsSync);
