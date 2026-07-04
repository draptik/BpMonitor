// Ambient declarations for `tsc --checkJs` over the hand-written wwwroot JS
// (see tsconfig.json at the repo root, run via `mise run lint:ts`). Dev-time
// only — nothing here is served or shipped.

// The plot div once Plotly.newPlot has run: Plotly grafts its event API and
// state onto the element. Only the members our scripts touch are declared.
interface PlotlyChartElement extends HTMLElement {
  on(event: string, handler: (event: any) => void): void;
  data?: any[];
  layout?: any;
  _fullLayout?: any;
}

// Vendored plotly-2.27.1.min.js global. `any` on purpose: typing the full
// Plotly surface is not worth it for the handful of calls we make.
declare const Plotly: any;

// wwwroot/plot-ready.js — classic scripts share one global scope, but tsc
// treats each file as its own module-less script, so cross-file functions
// need an ambient declaration.
declare function whenPlotReady(fn: (d: PlotlyChartElement) => void): void;

// wwwroot/theme.js assigns this onto window for the inline onclick handler.
interface Window {
  toggleTheme: () => void;
}
