// Links the Medications Timeline chart (Charts.fs BpChart.toHtmlMedications) to the BP
// chart above it (Wegier et al. 2021 Fig. 5): keeps their x-axes in sync on pan/zoom, and
// mirrors the scrubber (green spike line) between them in both directions. Waits for both
// plots via whenPlotReady (plot-ready.js) — index 0 is always the BP chart, index 1 the
// timeline, per the DOM order ReadingViews.fs renders them in.
//
// /history has no scrubber, so the timeline→BP hover-mirroring below is gated on
// `.value-strip` — only the axis-sync half does anything there.
function setupMedicationsSync() {
  const timelineDetails = /** @type {HTMLDetailsElement | null} */ (
    document.querySelector(".medications-timeline")
  );
  if (!timelineDetails) return;

  // Set during a synthetic dispatch below, so the mirrored plotly_hover doesn't bounce back and recurse.
  let syncing = false;

  /** @param {PlotlyChartElement} target @param {string} x */
  function hoverAt(target, x) {
    const geo = chartGeometry(target);
    if (!geo) return;
    const { xaxis, dragRect, br } = geo;
    const xPx = xaxis.l2p(xaxis.d2l(x));
    const yPx = geo.yaxis?.l2p?.(0) ?? br.height / 2;
    // A collapsed target chart is zero-width, so l2p yields NaN — Firefox throws on that.
    if (!Number.isFinite(xPx) || !Number.isFinite(yPx)) return;
    syncing = true;
    try {
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

      // SpikeSnap.Data snaps to the nearest reading across all loaded data, not just the
      // visible range — a sparse spot near the edge can snap off-screen. Cancel that.
      const spike = target.querySelector(".spikeline");
      if (spike) {
        const spikeRect = spike.getBoundingClientRect();
        if (spikeRect.right < br.left || spikeRect.left > br.left + br.width) {
          dragRect.dispatchEvent(new MouseEvent("mouseout", { bubbles: true }));
        }
      }
    } finally {
      syncing = false;
    }
  }

  /** @param {PlotlyChartElement} target */
  function unhover(target) {
    const dragRect = chartGeometry(target)?.dragRect;
    if (!dragRect) return;
    syncing = true;
    try {
      dragRect.dispatchEvent(new MouseEvent("mouseout", { bubbles: true }));
    } finally {
      syncing = false;
    }
  }

  // Outside the draglayer, p2d extrapolates past the visible range — reject it.
  /** @param {PlotlyChartElement} plot @param {number} clientX @returns {string | undefined} */
  function pointerX(plot, clientX) {
    const geo = chartGeometry(plot);
    if (!geo) return undefined;
    const relX = clientX - geo.br.left;
    if (relX < 0 || relX > geo.br.width) return undefined;
    return geo.xaxis.p2d(relX);
  }

  whenPlotReady((bpPlot) => {
    whenPlotReady((timelinePlot) => {
      // Setup runs on every htmx:afterSettle; skip plots already wired so a settle
      // that doesn't swap the chart can't stack duplicate handlers.
      if (timelinePlot.dataset.medicationsSyncBound) return;
      timelinePlot.dataset.medicationsSyncBound = "1";

      // The BP chart's y-axis title makes Plotly auto-expand its margin beyond the
      // configured value (`_fullLayout._size`, not `_fullLayout.margin`) — copy the
      // real margin onto the timeline so the x-axes align pixel-for-pixel. Also copies
      // the BP chart's actual x-axis range, since /history's BP chart autoranges.
      function syncTimelineToBp() {
        const bpSize = bpPlot._fullLayout._size;

        Plotly.relayout(timelinePlot, {
          "margin.l": bpSize.l,
          "margin.r": bpSize.r,
          "xaxis.range": bpPlot._fullLayout.xaxis.range,
        });
      }

      syncTimelineToBp();

      // /history wraps the BP chart in its own collapsed-by-default <details>; Plotly
      // renders at zero width while hidden, so re-sync once it's opened.
      const bpDetails = bpPlot.closest("details");

      if (bpDetails) {
        bpDetails.addEventListener("toggle", () => {
          if (bpDetails.open) {
            Plotly.Plots.resize(bpPlot);
            syncTimelineToBp();
          }
        });
      }

      // BP chart → timeline: mirror the spike position. A relayout-driven hover replay
      // (e.g. theme.js's applyChartTheme) can fire with no points.
      bpPlot.on("plotly_hover", (e) => {
        if (syncing) return;
        const x = e.points?.[0]?.x;
        if (x !== undefined) hoverAt(timelinePlot, x);
      });
      bpPlot.on("plotly_unhover", () => {
        if (!syncing) unhover(timelinePlot);
      });

      // Timeline → BP chart: hovering a medication bar moves the BP chart's spike (and,
      // via recent-scrubber.js's existing plotly_hover listener, the value strip's box).
      // fills-hover fires once on entry, not per move — mousemove below tracks while `hoveringBar`.
      const hasValueStrip = !!document.querySelector(".value-strip"); // /recent-only; /history has no spike
      let hoveringBar = false;

      // hoverAt's mouseout-then-mousemove reset can outrace its own redraw when fired on
      // every raw mousemove — coalescing to one dispatch per frame avoids that flicker.
      let pendingX = /** @type {string | undefined} */ (undefined);
      let rafScheduled = false;
      /** @param {string} x */
      function scheduleHoverAt(x) {
        pendingX = x;
        if (rafScheduled) return;
        rafScheduled = true;
        requestAnimationFrame(() => {
          rafScheduled = false;
          if (pendingX !== undefined) hoverAt(bpPlot, pendingX);
        });
      }

      timelinePlot.on("plotly_hover", (e) => {
        if (!hasValueStrip || syncing) return;
        hoveringBar = true;
        const x = e.points?.[0]?.x ?? (e.event && pointerX(timelinePlot, e.event.clientX));
        if (x !== undefined) scheduleHoverAt(x);
      });
      timelinePlot.on("plotly_unhover", () => {
        hoveringBar = false;
        pendingX = undefined;
        if (hasValueStrip && !syncing) unhover(bpPlot);
      });
      timelinePlot.addEventListener("mousemove", (e) => {
        if (!hasValueStrip || syncing || !hoveringBar) return;
        const x = pointerX(timelinePlot, e.clientX);
        if (x !== undefined) scheduleHoverAt(x);
      });

      // Pan/zoom (including the Last 7/30 days buttons, via recent-zoom.js's
      // Plotly.relayout call) — the timeline's x-axis is FixedRange (Charts.fs
      // medicationsXAxis), so it only ever follows the BP chart, never the reverse.
      bpPlot.on("plotly_relayout", (e) => {
        let lo = e["xaxis.range[0]"];
        let hi = e["xaxis.range[1]"];

        if (lo === undefined && Array.isArray(e["xaxis.range"])) {
          lo = e["xaxis.range"][0];
          hi = e["xaxis.range"][1];
        }

        // Double-click reset emits `xaxis.autorange` instead of a range — fall back to
        // the BP chart's now-current range so the timeline follows the reset too.
        if (lo === undefined && e["xaxis.autorange"] && bpPlot._fullLayout?.xaxis?.range) {
          [lo, hi] = bpPlot._fullLayout.xaxis.range;
        }

        if (lo === undefined || hi === undefined) return;

        Plotly.relayout(timelinePlot, { "xaxis.range": [lo, hi] });
      });

      // Resize on open (Plotly renders at zero width while hidden) and re-sync, in
      // case the BP chart was still closed when this copy above ran.
      timelineDetails.addEventListener("toggle", () => {
        if (timelineDetails.open) {
          Plotly.Plots.resize(timelinePlot);
          syncTimelineToBp();
        }
      });

      // details-memory.js may have already restored an open state (from localStorage)
      // before this async setup finished — that toggle fired before the listener above
      // existed to catch it, so resize once now to cover that case.
      if (timelineDetails.open) {
        Plotly.Plots.resize(timelinePlot);
        syncTimelineToBp();
      }
    }, 1);
  }, 0);
}

document.addEventListener("DOMContentLoaded", setupMedicationsSync);
document.addEventListener("htmx:afterSettle", setupMedicationsSync);
