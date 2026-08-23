// Shared "wait for Plotly" helper — the single copy of the poll-until-ready
// pattern previously duplicated by recent-scrubber.js, recent-zoom.js, and the
// (since-extracted) inline error-bar script in Charts.fs.
//
// The chart's own render script (Plotly.NET's Plotly.newPlot call, inlined in
// the chart HTML fragment) runs synchronously when parsed, but Plotly attaches
// its event API (`.on`) to the plot div asynchronously — so callers must poll
// until the div exists AND has `.on`. Callers are responsible for only invoking
// this on pages that actually render a chart; otherwise the poll never resolves.
//
// `index` picks which `.js-plotly-plot` on the page to wait for (default 0, the
// first one in document order). /recent and /history render the BP chart before
// the Medications Timeline chart (see ReadingViews.fs / ViewLayout — load-bearing
// DOM order), so index 0 is always the BP chart and index 1 the timeline, when present.
//
// Must load before any script that calls it (see ViewLayout.fs htmlHead order).

/**
 * @param {(d: PlotlyChartElement) => void} fn
 * @param {number} [index]
 */
// biome-ignore lint/correctness/noUnusedVariables: shared global, called by the other wwwroot chart scripts
function whenPlotReady(fn, index = 0) {
  function poll() {
    const d = /** @type {PlotlyChartElement | null} */ (
      document.querySelectorAll(".js-plotly-plot")[index]
    );
    if (!d?.on) {
      setTimeout(poll, 50);
      return;
    }
    fn(d);
  }
  // Deferred a tick so the chart fragment's own inline render script (parsed
  // later in the body) gets a chance to run first.
  setTimeout(poll, 0);
}

// Resolves a plot's axis converters and drag-layer rect for data-x → pixel conversion; shared by recent-scrubber.js and medications-sync.js.
/** @param {PlotlyChartElement} d */
// biome-ignore lint/correctness/noUnusedVariables: shared global, called by the other wwwroot chart scripts
function chartGeometry(d) {
  const xaxis = d._fullLayout?.xaxis;
  const yaxis = d._fullLayout?.yaxis;
  if (!xaxis?.d2l || !xaxis?.l2p) return null;
  const dragRect = d.querySelector(".draglayer .xy > rect");
  if (!dragRect) return null;
  return { xaxis, yaxis, dragRect, br: dragRect.getBoundingClientRect() };
}
