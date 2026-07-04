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
// Must load before any script that calls it (see ViewLayout.fs htmlHead order).

/** @param {(d: PlotlyChartElement) => void} fn */
// biome-ignore lint/correctness/noUnusedVariables: shared global, called by the other wwwroot chart scripts
function whenPlotReady(fn) {
  function poll() {
    const d = /** @type {PlotlyChartElement | null} */ (
      document.querySelector(".js-plotly-plot")
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
