// Runs once on initial load; survives hx-boost navigations because it lives in <head>.
(()=> {
  var t=localStorage.getItem('theme')||(window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light');
  document.documentElement.setAttribute('data-theme',t);
})();

function applyChartTheme(theme) {
  if (typeof Plotly === 'undefined') return;
  var isDark = theme === 'dark';
  document.querySelectorAll('.js-plotly-plot').forEach((d) => {
    Plotly.relayout(d, {
      paper_bgcolor: 'rgba(0,0,0,0)',
      plot_bgcolor: 'rgba(0,0,0,0)',
      font: { color: isDark ? '#c2cfd6' : '#444' },
      'yaxis.gridcolor': isDark ? 'rgba(194,207,214,0.12)' : 'rgba(0,0,0,0.08)'
    });
  });
}

window.toggleTheme=()=> {
  var h=document.documentElement,n=h.getAttribute('data-theme')==='dark'?'light':'dark';
  h.setAttribute('data-theme',n);
  localStorage.setItem('theme',n);
  var b=document.getElementById('theme-toggle');
  if(b)b.textContent=n==='dark'?'☀️':'🌙';
  applyChartTheme(n);
};

document.addEventListener('DOMContentLoaded',() => {
  applyChartTheme(document.documentElement.getAttribute('data-theme')||'light');
});
document.addEventListener('htmx:afterSettle',() => {
  applyChartTheme(document.documentElement.getAttribute('data-theme')||'light');
});
