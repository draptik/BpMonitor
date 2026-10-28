// Runs once on initial load; survives hx-boost navigations because it lives in <head>.
(()=> {
  const t=localStorage.getItem('theme')||(window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light');
  document.documentElement.setAttribute('data-theme',t);
})();

/** @param {string} theme */
function applyChartTheme(theme) {
  if (typeof Plotly === 'undefined') return;
  const isDark = theme === 'dark';
  const axisLineColor = isDark ? '#c2cfd6' : '#444';
  // Read after data-theme is set, so Pico's tokens already resolve to the new theme.
  const css = getComputedStyle(document.documentElement);
  document.querySelectorAll('.js-plotly-plot').forEach((d) => {
    Plotly.relayout(d, {
      paper_bgcolor: 'rgba(0,0,0,0)',
      plot_bgcolor: 'rgba(0,0,0,0)',
      font: { color: axisLineColor },
      'xaxis.linecolor': axisLineColor,
      'yaxis.linecolor': axisLineColor,
      'xaxis.tickcolor': axisLineColor,
      'yaxis.tickcolor': axisLineColor,
      'yaxis.gridcolor': isDark ? 'rgba(194,207,214,0.12)' : 'rgba(0,0,0,0.08)',
      'modebar.bgcolor': 'rgba(0,0,0,0)',
      'modebar.color': css.getPropertyValue('--pico-muted-color').trim(),
      'modebar.activecolor': css.getPropertyValue('--pico-primary').trim()
    });
    restyleMedicationColors(/** @type {PlotlyChartElement} */ (d), isDark);
  });
}

// Medications Timeline traces carry "light|dark" hexes in `meta` (Charts.fs); swap in the right half.
/** @param {PlotlyChartElement} plot @param {boolean} isDark */
function restyleMedicationColors(plot, isDark) {
  const metas = (plot.data || []).map((/** @type {any} */ tr) => tr.meta);
  if (metas.length === 0 || !metas.every((/** @type {any} */ m) => typeof m === 'string' && m.includes('|'))) return;
  const colors = metas.map((/** @type {string} */ m) => m.split('|')[isDark ? 1 : 0]);
  Plotly.restyle(plot, { 'line.color': colors, 'hoverlabel.bgcolor': colors });
}

// Tooltip names the theme a click switches to; the icon itself is swapped by CSS.
function syncThemeToggles() {
  const t=document.documentElement.getAttribute('data-theme')==='dark'?'dark':'light';
  document.querySelectorAll('.theme-toggle').forEach((b) => {
    const label=b.getAttribute(`data-tooltip-${t}`)||'';
    b.setAttribute('title',label);
    b.setAttribute('aria-label',label);
  });
}

window.toggleTheme=()=> {
  const h=document.documentElement;
  const n=h.getAttribute('data-theme')==='dark'?'light':'dark';
  h.setAttribute('data-theme',n);
  localStorage.setItem('theme',n);
  syncThemeToggles();
  applyChartTheme(n);
};

document.addEventListener('DOMContentLoaded',() => {
  syncThemeToggles();
  applyChartTheme(document.documentElement.getAttribute('data-theme')||'light');
});
// hx-boost swaps and Back/Forward restores bring toggles whose tooltip may not match the theme.
document.addEventListener('htmx:afterSwap',syncThemeToggles);
document.addEventListener('htmx:historyRestore',syncThemeToggles);
document.addEventListener('htmx:afterSettle',() => {
  applyChartTheme(document.documentElement.getAttribute('data-theme')||'light');
});
