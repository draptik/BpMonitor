// Scrolls the active sub-period pill into view. The trends panel is an htmx-swapped
// fragment, so this re-runs on htmx:afterSettle (not just DOMContentLoaded) to catch
// every swap — same pattern as theme.js for surviving hx-boost navigations.
function scrollActiveSubPeriodIntoView() {
  if (!document.querySelector(".trends-subperiod-buttons")) return;
  var active = document.querySelector('.trends-subperiod-buttons [aria-current="page"]');
  if (active) active.scrollIntoView({ inline: "nearest", block: "nearest" });
}

document.addEventListener("DOMContentLoaded", scrollActiveSubPeriodIntoView);
document.addEventListener("htmx:afterSettle", scrollActiveSubPeriodIntoView);
