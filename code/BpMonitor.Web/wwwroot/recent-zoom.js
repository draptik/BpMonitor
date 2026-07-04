// "Last 7 days" / "Last 30 days" shortcut buttons (ReadingViews.fs `zoomButtons`).
// Each button carries its target range as data-lo/data-hi (server-rendered via
// Formats.formatLocal, anchored to the same `now` the chart's own initial range
// uses), so clicking it just replays the chart's own range format through
// Plotly.relayout — same call pattern as theme.js. The existing plotly_relayout
// listener in recent-scrubber.js re-syncs the value strip automatically. The
// server renders the initial active pill (aria-pressed="true" on whichever
// button matches the chart's opening window, see ReadingViews.fs `zoomButton`);
// clicking here re-toggles it client-side, since the chart itself never
// round-trips to the server.
function setupRecentZoomButtons() {
  const buttons = /** @type {NodeListOf<HTMLElement>} */ (
    document.querySelectorAll(".recent-zoom-button")
  );
  if (buttons.length === 0) return;

  whenPlotReady((d) => {
    buttons.forEach((button) => {
      button.addEventListener("click", () => {
        Plotly.relayout(d, { "xaxis.range": [button.dataset.lo, button.dataset.hi] });
        buttons.forEach((b) => {
          b.setAttribute("aria-pressed", b === button ? "true" : "false");
        });
      });
    });
  });
}

document.addEventListener("DOMContentLoaded", setupRecentZoomButtons);
document.addEventListener("htmx:afterSettle", setupRecentZoomButtons);
