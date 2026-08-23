# ADR-0009: JS Unit Testing for wwwroot Scripts (Rejected)

## Status

Rejected

## Context

`wwwroot/` has ~620 lines of hand-written JS across 9 files (`theme.js`, `plot-ready.js`,
`chart-hover.js`, `recent-scrubber.js`, `recent-zoom.js`, `trends-scroll.js`,
`medications-sync.js`, `details-memory.js`, `theme-label.js`), all thin DOM/Plotly glue: event
wiring, hover synchronization, and axis-range math. It already ships with two forms of static
safety net wired into `mise run lint` and CI: Biome linting and `tsc --checkJs` type-checking
via JSDoc (`tsconfig.json`). Adding a JS unit test runner (e.g. Vitest) was considered as a
third layer.

## Finding

The logic worth testing in this JS — e.g. `recent-scrubber.js`'s pixel-distance hover matching,
`medications-sync.js`'s axis-range mirroring via `bpPlot._fullLayout` — is coupled to Plotly's
internal runtime object shape, real DOM geometry (`getBoundingClientRect`), and synthetic mouse
events dispatched onto Plotly's drag layer. jsdom does not compute real layout, so
geometry-dependent code would need hand-built mocks of both jsdom's box model and Plotly's
internals. Tests built on those mocks assert against invented behavior, not real Plotly
behavior — passing today and failing to catch a real Plotly upgrade breaking the app tomorrow.

`BpMonitor.Web.E2E.Tests` already exercises this exact class of interaction at the right level:
a real Chromium browser driving the real app against the real, unmocked Plotly runtime, wired
into CI. It already caught and drove the fix in the medications-sync/BP-chart double-click reset
bug (#451) — the kind of defect a jsdom+mocked-Plotly unit test would most likely have missed
or given false confidence about.

## Decision

**Not pursued.** Biome + `tsc --checkJs` already cover typos, wrong property access, and
null-safety — the errors this JS is actually prone to. The remaining risk (Plotly integration
behavior) is already covered, and covered more faithfully, by the existing E2E suite. A new
test runner would mean a new npm dependency tree, a new CI job, a new `mise` task, and ongoing
jsdom/Plotly mock maintenance, for code that has had no bugs traceable to a gap this layer would
have closed.

## Consequences

- `wwwroot/` JS continues to rely on Biome + `tsc --checkJs` for static safety and
  `BpMonitor.Web.E2E.Tests` for behavioral coverage; no jsdom/Vitest layer is introduced.
- Revisit if the client-side JS grows real logic that doesn't require a live browser to verify
  (e.g. a pure computation or state machine extracted out of the DOM/Plotly glue) — that kind of
  code would be cheap to unit test without mocking Plotly or DOM geometry.
