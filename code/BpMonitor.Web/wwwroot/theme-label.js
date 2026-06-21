// Re-runs on every body render (initial + hx-boost swaps) to sync the button icons.
(()=> {var n=document.documentElement.getAttribute('data-theme')==='dark'?'☀️':'🌙';document.querySelectorAll('.theme-toggle').forEach((b) => { b.textContent=n; });})();
