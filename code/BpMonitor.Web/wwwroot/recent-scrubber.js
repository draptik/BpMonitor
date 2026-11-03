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
// ReadingViews.fs `valueStrip` and the readings section); `plotly_relayout` syncs both.
//
// The link is symmetric: hovering the strip drives the chart's spike via
// Plotly.Fx.hover (strip → chart), mirroring the existing chart → strip direction.
// Event delegation on the strip container avoids flicker when the pointer moves
// between the stacked Systolic/Diastolic cells of the same column.
function setupRecentScrubber() {
  const strip = document.querySelector(".value-strip");
  if (!strip) return;

  whenPlotReady((d) => {
    // Setup runs on every htmx:afterSettle; skip plots already wired so a settle
    // that doesn't swap the chart can't stack duplicate handlers on the same div.
    if (d.dataset.scrubberBound) return;
    d.dataset.scrubberBound = "1";

    // chart → strip: box the value-strip column matching the hovered chart point.
    d.on("plotly_hover", (e) => {
      // A relayout-driven hover replay (e.g. theme.js's applyChartTheme) can fire with no points.
      const x = e.points?.[0]?.x;
      if (x === undefined) return;
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
    // Matched by `meta` (Charts.fs commentTraces), not the localized trace name.
    const traces = d.data ?? [];
    const commentTraceIndex = traces.findIndex((t) => t.meta === "comments");
    if (commentTraceIndex !== -1) {
      const commentXs = traces[commentTraceIndex].x;
      const commentTexts = traces[commentTraceIndex].text;

      // The tooltip lives on <body>, outside the htmx-swapped #recent-chart region,
      // so drop the previous plot's tooltip before appending this one.
      document.querySelector(".comment-tooltip")?.remove();
      const tooltip = document.createElement("div");
      tooltip.className = "comment-tooltip";
      const tooltipText = document.createElement("div");
      const tooltipTime = document.createElement("div");
      tooltipTime.className = "comment-tooltip-time";
      tooltip.append(tooltipText, tooltipTime);
      document.body.appendChild(tooltip);

      const proximityPx = 8;

      // xaxis.d2l is a pure date-string → linear-ms conversion that never changes
      // for the plot's data (only l2p depends on zoom/pan), so convert each comment
      // x once on the first mousemove instead of re-parsing every marker per event.
      /** @type {number[] | null} */
      let commentLs = null;

      d.addEventListener("mousemove", (e) => {
        const geo = chartGeometry(d);
        if (!geo?.yaxis?.l2p) return;
        const { xaxis, yaxis, br } = geo;
        const yPx = br.top + yaxis.l2p(0);
        const ls =
          commentLs ?? commentXs.map((/** @type {string} */ x) => xaxis.d2l(x));
        commentLs = ls;

        let nearest = -1;
        let nearestXPx = 0;
        let nearestDist = Infinity;
        for (let i = 0; i < ls.length; i++) {
          const xPx = br.left + xaxis.l2p(ls[i]);
          const dist = Math.hypot(e.clientX - xPx, e.clientY - yPx);
          if (dist < nearestDist) {
            nearestDist = dist;
            nearest = i;
            nearestXPx = xPx;
          }
        }

        if (nearestDist <= proximityPx) {
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
      // y = 120 mmHg sits mid-chart; the y-axis' 0 is its bottom edge, where a rounded pixel can miss.
      dispatchHoverAt(d, x, 120);
    });
    strip.addEventListener("mouseleave", () => {
      lastX = null;
      // Dispatch mouseout on the drag layer to trigger Plotly's unhover path;
      // also clear .scrubbed directly so the box never lingers.
      const dragRect = chartGeometry(d)?.dragRect;
      if (dragRect)
        dragRect.dispatchEvent(new MouseEvent("mouseout", { bubbles: true }));
      document.querySelectorAll(".value-strip td.scrubbed").forEach((c) => {
        c.classList.remove("scrubbed");
      });
    });

    d.on("plotly_relayout", (e) => {
      // Both the value strip's cells and the readings list's rows carry a data-x key.
      const elements = /** @type {NodeListOf<HTMLElement>} */ (
        document.querySelectorAll(".value-strip td[data-x], .recent-readings tr[data-x]")
      );
      const range = relayoutXRange(e, d);
      if (!range) return;
      const [lo, hi] = range;

      // Plotly's own d2l, not `new Date`: a midnight bound arrives as a bare "yyyy-MM-dd",
      // which `new Date` reads as UTC while reading the data-x keys as local time.
      const xaxis = d._fullLayout?.xaxis;
      if (!xaxis?.d2l) return;
      const loT = xaxis.d2l(lo);
      const hiT = xaxis.d2l(hi);
      if (!Number.isFinite(loT) || !Number.isFinite(hiT)) return;

      elements.forEach((el) => {
        const elX = el.dataset.x;
        if (!elX) return;
        const t = xaxis.d2l(elX);
        if (!Number.isFinite(t)) return;
        el.classList.toggle("out-of-range", t < loT || t > hiT);
      });
    });
  });
}

document.addEventListener("DOMContentLoaded", setupRecentScrubber);
document.addEventListener("htmx:afterSettle", setupRecentScrubber);
