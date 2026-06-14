// Runs once on initial load; survives hx-boost navigations because it lives in <head>.
(()=> {
  var t=localStorage.getItem('theme')||(window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light');
  document.documentElement.setAttribute('data-theme',t);
})();

function updateChartIframes(theme) {
  var chartHeight = getComputedStyle(document.documentElement).getPropertyValue('--chart-height').trim() || '620px';
  document.querySelectorAll('[data-chart-src]').forEach(function(el) {
    var base = el.getAttribute('data-chart-src');
    el.src = base + (base.includes('?') ? '&' : '?') + 'theme=' + theme + '&height=' + chartHeight;
  });
}

window.toggleTheme=()=> {
  var h=document.documentElement,n=h.getAttribute('data-theme')==='dark'?'light':'dark';
  h.setAttribute('data-theme',n);
  localStorage.setItem('theme',n);
  var b=document.getElementById('theme-toggle');
  if(b)b.textContent=n==='dark'?'Light':'Dark';
  updateChartIframes(n);
};

document.addEventListener('DOMContentLoaded',function() {
  updateChartIframes(document.documentElement.getAttribute('data-theme')||'light');
});
document.addEventListener('htmx:afterSettle',function() {
  updateChartIframes(document.documentElement.getAttribute('data-theme')||'light');
});
