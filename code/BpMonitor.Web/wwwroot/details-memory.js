// Remembers whether a <details data-persist-key="..."> element is open across page loads
// and htmx swaps (e.g. the Medications Timeline panel — collapsed by default, see
// MedicationViews.timelinePanel). Each key gets its own localStorage entry so multiple
// persisted <details> on a page (or across pages) don't clobber each other's state.
function restoreDetailsState() {
  const elements = /** @type {NodeListOf<HTMLDetailsElement>} */ (
    document.querySelectorAll("details[data-persist-key]")
  );
  elements.forEach((el) => {
    const key = el.dataset.persistKey;
    if (!key || el.dataset.persistBound) return;
    el.dataset.persistBound = "1";

    if (localStorage.getItem(`details-open:${key}`) === "1") {
      el.open = true;
    }

    el.addEventListener("toggle", () => {
      localStorage.setItem(`details-open:${key}`, el.open ? "1" : "0");
    });
  });
}

document.addEventListener("DOMContentLoaded", restoreDetailsState);
document.addEventListener("htmx:afterSettle", restoreDetailsState);
