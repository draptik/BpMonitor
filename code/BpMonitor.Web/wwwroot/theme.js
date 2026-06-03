// Runs once on initial load; survives hx-boost navigations because it lives in <head>.
(()=> {
  var t=localStorage.getItem('theme')||(window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light');
  document.documentElement.setAttribute('data-theme',t);
})();
window.toggleTheme=()=> {
  var h=document.documentElement,n=h.getAttribute('data-theme')==='dark'?'light':'dark';
  h.setAttribute('data-theme',n);
  localStorage.setItem('theme',n);
  var b=document.getElementById('theme-toggle');
  if(b)b.textContent=n==='dark'?'Light':'Dark';
};
