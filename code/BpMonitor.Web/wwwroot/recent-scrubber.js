// Fig. 5's scrubber bar (Wegier et al. 2021): the chart's x-axis spike (Charts.fs
// `recentXAxis`) already draws the moving vertical line; this links it to the value
// strip by boxing the hovered column. Follows the same poll-until-ready pattern as
// the chart's own errorBarScript (BpMonitor.Charts/Charts.fs).
//
// The strip lists every loaded reading (the chart's load window — bounded but wider
// than the visible focus). Each cell is tagged with the same x-label the chart uses
// for that reading (Charts.fs `seriesOf` formats x as Formats.formatLocal r.Timestamp),
// so a hovered chart point can be matched back to its strip column via `data-x`.
//
// Columns outside the visible focus window start hidden via `out-of-range` (see
// ReadingViews.fs `valueStrip`); `plotly_relayout` fires with `xaxis.range[0]`/`[1]`
// on zoom/pan, or `xaxis.autorange` on a reset (e.g. double-click), and this keeps
// the value strip in sync as the user pans/zooms the chart.
function setupRecentScrubber() {
  if (!document.querySelector(".value-strip")) return;

  function setup() {
    var d = document.querySelector(".js-plotly-plot");
    if (!d?.on) {
      setTimeout(setup, 50);
      return;
    }

    d.on("plotly_hover", (e) => {
      var x = e.points[0].x;
      document.querySelectorAll(".value-strip td.scrubbed").forEach((c) => {
        c.classList.remove("scrubbed");
      });
      document.querySelectorAll(`.value-strip td[data-x="${x}"]`).forEach((c) => {
        c.classList.add("scrubbed");
      });
    });

    d.on("plotly_unhover", () => {
      document.querySelectorAll(".value-strip td.scrubbed").forEach((c) => {
        c.classList.remove("scrubbed");
      });
    });

    d.on("plotly_relayout", (e) => {
      var cells = document.querySelectorAll(".value-strip td[data-x]");
      var lo = e["xaxis.range[0]"];
      var hi = e["xaxis.range[1]"];

      if (lo === undefined && Array.isArray(e["xaxis.range"])) {
        lo = e["xaxis.range"][0];
        hi = e["xaxis.range"][1];
      }

      if (lo === undefined && e["xaxis.autorange"] === undefined && d.layout?.xaxis) {
        if (d.layout.xaxis.autorange) {
          lo = undefined;
        } else if (d.layout.xaxis.range) {
          lo = d.layout.xaxis.range[0];
          hi = d.layout.xaxis.range[1];
        }
      }

      if (lo === undefined || hi === undefined) {
        cells.forEach((c) => {
          c.classList.remove("out-of-range");
        });
        return;
      }

      var loT = new Date(String(lo).replace(" ", "T")).getTime();
      var hiT = new Date(String(hi).replace(" ", "T")).getTime();
      if (Number.isNaN(loT) || Number.isNaN(hiT)) return;

      cells.forEach((c) => {
        var t = new Date(c.dataset.x.replace(" ", "T")).getTime();
        if (Number.isNaN(t)) return;
        c.classList.toggle("out-of-range", t < loT || t > hiT);
      });
    });
  }

  setTimeout(setup, 0);
}

document.addEventListener("DOMContentLoaded", setupRecentScrubber);
document.addEventListener("htmx:afterSettle", setupRecentScrubber);
