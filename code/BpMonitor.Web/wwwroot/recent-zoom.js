// "Last 7 days" / "Last 30 days" shortcut buttons (ReadingViews.fs `zoomButtons`).
// Each button carries its target range as data-lo/data-hi (server-rendered via
// Formats.formatLocal, anchored to the same `now` the chart's own initial range
// uses), so clicking it just replays the chart's own range format through
// Plotly.relayout — same call pattern as theme.js. The existing plotly_relayout
// listener in recent-scrubber.js re-syncs the value strip automatically.
function setupRecentZoomButtons() {
  var buttons = document.querySelectorAll(".recent-zoom-button");
  if (buttons.length === 0) return;

  function setup() {
    var d = document.querySelector(".js-plotly-plot");
    if (!d?.on) {
      setTimeout(setup, 50);
      return;
    }

    buttons.forEach((button) => {
      button.addEventListener("click", () => {
        Plotly.relayout(d, { "xaxis.range": [button.dataset.lo, button.dataset.hi] });
      });
    });
  }

  setup();
}

document.addEventListener("DOMContentLoaded", setupRecentZoomButtons);
document.addEventListener("htmx:afterSettle", setupRecentZoomButtons);
