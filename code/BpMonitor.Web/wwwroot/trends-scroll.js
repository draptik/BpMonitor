// Scrolls the active sub-period pill into view, centered in the ~5-pill window. The
// trends panel is an htmx-swapped fragment, so this re-runs on htmx:afterSettle (not
// just DOMContentLoaded) to catch every swap — same pattern as theme.js for surviving
// hx-boost navigations.
function scrollActiveSubPeriodIntoView() {
  const row = document.querySelector(".trends-subperiod-buttons");
  if (!row) return;
  const active = row.querySelector('[aria-current="page"]');
  if (active) active.scrollIntoView({ inline: "center", block: "nearest" });
}

// Toggles edge-fade affordances (CSS, app.css `.trends-subperiod-scroller`) on the
// non-scrolling wrapper based on how far the pill row has been scrolled.
function updateSubPeriodFades() {
  const row = document.querySelector(".trends-subperiod-buttons");
  const scroller = document.querySelector(".trends-subperiod-scroller");
  if (!row || !scroller) return;

  const tolerancePx = 1;
  const canScrollLeft = row.scrollLeft > tolerancePx;
  const canScrollRight = row.scrollLeft + row.clientWidth < row.scrollWidth - tolerancePx;

  scroller.classList.toggle("can-scroll-left", canScrollLeft);
  scroller.classList.toggle("can-scroll-right", canScrollRight);
}

function refreshSubPeriodScroller() {
  scrollActiveSubPeriodIntoView();
  updateSubPeriodFades();
}

document.addEventListener("DOMContentLoaded", refreshSubPeriodScroller);
document.addEventListener("htmx:afterSettle", refreshSubPeriodScroller);
window.addEventListener("resize", updateSubPeriodFades);

document.addEventListener(
  "scroll",
  (event) => {
    if (event.target?.classList?.contains("trends-subperiod-buttons")) {
      updateSubPeriodFades();
    }
  },
  true,
);
