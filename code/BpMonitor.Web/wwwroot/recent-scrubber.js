// Fig. 5's scrubber bar (Wegier et al. 2021): the chart's x-axis spike (Charts.fs
// `recentXAxis`) already draws the moving vertical line; this links it to the value
// strip by boxing the hovered column. Waits for the chart via whenPlotReady
// (plot-ready.js), like the other chart scripts.
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

  // Resolves the chart's axis converters and drag-layer bounding rect — shared by the
  // comment-tooltip mousemove handler and the strip → chart mouseover handler below,
  // both of which need to convert a data x-value to a viewport pixel position. Queried
  // fresh on each call (not cached) since the drag-layer rect can shift on scroll/resize.
  /** @param {PlotlyChartElement} d */
  function chartGeometry(d) {
    const xaxis = d._fullLayout?.xaxis;
    const yaxis = d._fullLayout?.yaxis;
    if (!xaxis?.d2l || !xaxis?.l2p) return null;
    const dragRect = d.querySelector(".draglayer .xy > rect");
    if (!dragRect) return null;
    return { xaxis, yaxis, dragRect, br: dragRect.getBoundingClientRect() };
  }

  whenPlotReady((d) => {
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

    // Comment tooltip: /recent uses unified hover (Charts.fs HoverMode.X), which finds
    // the nearest point at the hovered x-column across every trace — so without this,
    // the comment would surface whenever the cursor is anywhere near its x-column, not
    // only when directly over its marker. The comment trace is set to skip native hover
    // (Charts.fs commentTraces via renderRecent) and this custom tooltip drives it
    // instead, checking actual pixel distance to each marker on every mousemove.
    // Position is set via left/top plus a CSS transform (not measured dimensions) so it
    // works correctly even while the tooltip starts hidden (offsetHeight is 0).
    //
    // Matched by trace name — keep this in sync with the `Name = "Comments"` trace built
    // in Charts.fs `commentTraces`.
    const traces = d.data ?? [];
    const commentTraceIndex = traces.findIndex((t) => t.name === "Comments");
    if (commentTraceIndex !== -1) {
      const commentXs = traces[commentTraceIndex].x;
      const commentTexts = traces[commentTraceIndex].text;

      const tooltip = document.createElement("div");
      tooltip.className = "comment-tooltip";
      const tooltipText = document.createElement("div");
      const tooltipTime = document.createElement("div");
      tooltipTime.className = "comment-tooltip-time";
      tooltip.append(tooltipText, tooltipTime);
      document.body.appendChild(tooltip);

      const proximityPx = 8;

      d.addEventListener("mousemove", (e) => {
        const geo = chartGeometry(d);
        if (!geo?.yaxis?.l2p) return;
        const { xaxis, yaxis, br } = geo;
        const yPx = br.top + yaxis.l2p(0);

        let nearest = -1;
        let nearestXPx = 0;
        let nearestDist = Infinity;
        for (let i = 0; i < commentXs.length; i++) {
          const xPx = br.left + xaxis.l2p(xaxis.d2l(commentXs[i]));
          const dist = Math.hypot(e.clientX - xPx, e.clientY - yPx);
          if (dist < nearestDist) {
            nearestDist = dist;
            nearest = i;
            nearestXPx = xPx;
          }
        }

        if (nearest !== -1 && nearestDist <= proximityPx) {
          tooltipText.textContent = commentTexts[nearest];
          tooltipTime.textContent = commentXs[nearest];
          tooltip.style.left = `${nearestXPx}px`;
          tooltip.style.top = `${yPx}px`;
          tooltip.style.display = "block";
        } else {
          tooltip.style.display = "none";
        }
      });

      d.addEventListener("mouseleave", () => {
        tooltip.style.display = "none";
      });
    }

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
    /** @type {string | null} */
    let lastX = null;
    strip.addEventListener("mouseover", (e) => {
      if (!(e.target instanceof Element)) return;
      const cell = /** @type {HTMLElement | null} */ (e.target.closest("td[data-x]"));
      const x = cell?.dataset.x;
      if (!x || x === lastX) return;
      lastX = x;
      const geo = chartGeometry(d);
      if (!geo) return;
      const { xaxis, dragRect, br } = geo;
      const xPx = xaxis.l2p(xaxis.d2l(x));
      const yPx = geo.yaxis?.l2p?.(120) ?? br.height / 2;
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
      const cells = /** @type {NodeListOf<HTMLElement>} */ (
        document.querySelectorAll(".value-strip td[data-x]")
      );
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
        const cellX = c.dataset.x;
        if (!cellX) return;
        const t = new Date(cellX.replace(" ", "T")).getTime();
        if (Number.isNaN(t)) return;
        c.classList.toggle("out-of-range", t < loT || t > hiT);
      });
    });
  });
}

document.addEventListener("DOMContentLoaded", setupRecentScrubber);
document.addEventListener("htmx:afterSettle", setupRecentScrubber);
