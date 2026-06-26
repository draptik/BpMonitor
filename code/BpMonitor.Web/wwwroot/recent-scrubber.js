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
//
// The link is symmetric: hovering the strip drives the chart's spike via
// Plotly.Fx.hover (strip → chart), mirroring the existing chart → strip direction.
// Event delegation on the strip container avoids flicker when the pointer moves
// between the stacked Systolic/Diastolic cells of the same column.
function setupRecentScrubber() {
  const strip = document.querySelector(".value-strip");
  if (!strip) return;

  function setup() {
    const d = document.querySelector(".js-plotly-plot");
    if (!d?.on) {
      setTimeout(setup, 50);
      return;
    }

    // chart → strip: box the value-strip column matching the hovered chart point.
    d.on("plotly_hover", (e) => {
      const x = e.points[0].x;
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

    // strip → chart: hovering the strip moves the chart's spike to that column.
    // `out-of-range` cells are display:none and cannot be hovered, so no guard
    // needed for them. `lastX` avoids redundant dispatches when the pointer moves
    // between the stacked Systolic/Diastolic cells of the same column.
    //
    // We trigger Plotly's own hover machinery by dispatching a synthetic mousemove
    // on the chart's drag-layer rect at the pixel position of the data point.
    // The axis converters d2l/l2p (date-to-linear and linear-to-pixel) give the
    // correct x pixel without any timezone conversion (Plotly's own d2l parses the
    // same date strings it stored in the trace data). The plotly_hover event fires
    // naturally, so the existing chart→strip listener adds .scrubbed for free.
    let lastX = null;
    strip.addEventListener("mouseover", (e) => {
      const cell = e.target.closest("td[data-x]");
      if (!cell || cell.dataset.x === lastX) return;
      lastX = cell.dataset.x;
      const xaxis = d._fullLayout?.xaxis;
      if (!xaxis?.d2l || !xaxis?.l2p) return;
      const dragRect = d.querySelector(".draglayer .xy > rect");
      if (!dragRect) return;
      const br = dragRect.getBoundingClientRect();
      const xPx = xaxis.l2p(xaxis.d2l(cell.dataset.x));
      const yPx = (d._fullLayout?.yaxis?.l2p?.(120)) ?? br.height / 2;
      dragRect.dispatchEvent(
        new MouseEvent("mousemove", {
          bubbles: true,
          cancelable: true,
          clientX: br.left + xPx,
          clientY: br.top + yPx,
        }),
      );
    });
    strip.addEventListener("mouseleave", () => {
      lastX = null;
      // Dispatch mouseout on the drag layer to trigger Plotly's unhover path;
      // also clear .scrubbed directly so the box never lingers.
      const dragRect = d.querySelector(".draglayer .xy > rect");
      if (dragRect)
        dragRect.dispatchEvent(new MouseEvent("mouseout", { bubbles: true }));
      document.querySelectorAll(".value-strip td.scrubbed").forEach((c) => {
        c.classList.remove("scrubbed");
      });
    });

    d.on("plotly_relayout", (e) => {
      const cells = document.querySelectorAll(".value-strip td[data-x]");
      let lo = e["xaxis.range[0]"];
      let hi = e["xaxis.range[1]"];

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

      const loT = new Date(String(lo).replace(" ", "T")).getTime();
      const hiT = new Date(String(hi).replace(" ", "T")).getTime();
      if (Number.isNaN(loT) || Number.isNaN(hiT)) return;

      cells.forEach((c) => {
        const t = new Date(c.dataset.x.replace(" ", "T")).getTime();
        if (Number.isNaN(t)) return;
        c.classList.toggle("out-of-range", t < loT || t > hiT);
      });
    });
  }

  setTimeout(setup, 0);
}

document.addEventListener("DOMContentLoaded", setupRecentScrubber);
document.addEventListener("htmx:afterSettle", setupRecentScrubber);
