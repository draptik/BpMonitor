// Chart mount fix + error-bar hover highlight, shared by every chart page
// (/history, /recent, /trends). Extracted from the inline script Charts.fs
// used to append to each chart's HTML, so it is Biome-lintable and served as
// a normal static asset. Self-guards on the server-rendered `.chart` container
// (whenPlotReady polls forever, so it must not start on chartless pages) and
// re-runs on htmx:afterSettle to survive hx-boost navigations and the
// trends-panel fragment swaps — same pattern as the other wwwroot scripts.
function setupChartHover() {
  if (!document.querySelector(".chart")) return;

  whenPlotReady((d) => {
    // Setup runs on every htmx:afterSettle; skip plots already wired so a settle
    // that doesn't swap the chart can't stack duplicate handlers on the same div.
    if (d.dataset.hoverBound) return;
    d.dataset.hoverBound = "1";

    // Plotly's initial render ignores the `.chart` container's CSS height (it
    // lays out at its own content-driven default, ~450px) and only correctly
    // fits the actual container on a later resize event. Since `.chart` has
    // `overflow:hidden`, that mismatch clips the bottom of the chart — on
    // narrow mobile heights, severely enough to cut off the x-axis tick labels
    // entirely. Forcing one resize right after mount makes Plotly re-measure
    // the real (CSS-constrained) container immediately, instead of waiting for
    // a resize event that may never fire.
    Plotly.Plots.resize(d);

    d.on("plotly_hover", (e) => {
      const p = e.points[0];
      const gs = d.querySelectorAll("g.errorbars")[p.curveNumber];
      if (!gs) return;
      const bar = gs.querySelectorAll("g.errorbar")[p.pointIndex];
      if (!bar) return;
      const path = /** @type {SVGPathElement | null} */ (
        bar.querySelector("path.yerror")
      );
      if (path) path.style.setProperty("stroke-opacity", "1", "important");
    });

    d.on("plotly_unhover", () => {
      d.querySelectorAll("g.errorbars path.yerror").forEach((p) => {
        /** @type {SVGPathElement} */ (p).style.removeProperty("stroke-opacity");
      });
    });
  });
}

document.addEventListener("DOMContentLoaded", setupChartHover);
document.addEventListener("htmx:afterSettle", setupChartHover);
