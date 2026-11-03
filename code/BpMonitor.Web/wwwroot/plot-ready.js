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
function chartGeometry(d) {
  const xaxis = d._fullLayout?.xaxis;
  const yaxis = d._fullLayout?.yaxis;
  if (!xaxis?.d2l || !xaxis?.l2p) return null;
  const dragRect = d.querySelector(".draglayer .xy > rect");
  if (!dragRect) return null;
  return { xaxis, yaxis, dragRect, br: dragRect.getBoundingClientRect() };
}

// Fires Plotly's own hover at data `x` (and `y`, in y-axis units) via a synthetic mousemove on
// the drag layer; false when the chart isn't laid out. Shared by recent-scrubber.js and medications-sync.js.
/** @param {PlotlyChartElement} d @param {string} x @param {number} y @returns {boolean} */
// biome-ignore lint/correctness/noUnusedVariables: shared global, called by the other wwwroot chart scripts
function dispatchHoverAt(d, x, y) {
  const geo = chartGeometry(d);
  if (!geo) return false;
  const { xaxis, yaxis, dragRect, br } = geo;
  const xPx = xaxis.l2p(xaxis.d2l(x));
  const yPx = yaxis?.l2p?.(y) ?? br.height / 2;
  // A collapsed target chart is zero-width, so l2p yields NaN — Firefox throws on that.
  if (!Number.isFinite(xPx) || !Number.isFinite(yPx)) return false;
  // Resets Plotly's own hover state first — back-to-back mousemove dispatches without
  // this made Plotly's internal throttle emit every other one with an empty points array.
  dragRect.dispatchEvent(new MouseEvent("mouseout", { bubbles: true }));
  dragRect.dispatchEvent(
    new MouseEvent("mousemove", {
      bubbles: true,
      cancelable: true,
      clientX: br.left + xPx,
      clientY: br.top + yPx,
    }),
  );
  return true;
}

// The x-axis range after a plotly_relayout that touched it, else null. Read from the plot,
// not the event: a reset sends only `xaxis.autorange`, and a one-ended update only one bound.
/** @param {Record<string, unknown>} e @param {PlotlyChartElement} d @returns {[string, string] | null} */
// biome-ignore lint/correctness/noUnusedVariables: shared global, called by the other wwwroot chart scripts
function relayoutXRange(e, d) {
  const touchesX = Object.keys(e).some((k) => k.startsWith("xaxis.range") || k === "xaxis.autorange");
  if (!touchesX) return null;
  const range = d._fullLayout?.xaxis?.range;
  return Array.isArray(range) && range.length === 2 ? [range[0], range[1]] : null;
}
