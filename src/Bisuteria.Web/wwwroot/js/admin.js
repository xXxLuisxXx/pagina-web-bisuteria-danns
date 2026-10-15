(() => {
    const button = document.getElementById('adminTheme');
    function apply(dark) { document.documentElement.dataset.adminTheme = dark ? 'dark' : 'light'; button?.setAttribute('aria-pressed', String(dark)); }
    try { apply(localStorage.getItem('danns-admin-theme') === 'dark'); } catch { apply(false); }
    button?.addEventListener('click', () => { const dark = document.documentElement.dataset.adminTheme !== 'dark'; apply(dark); try { localStorage.setItem('danns-admin-theme', dark ? 'dark' : 'light'); } catch {} });
    document.querySelectorAll('main form[method=get]').forEach(form => form.addEventListener('submit', () => { const loader = document.querySelector('.admin-loading'); if (loader) loader.hidden = false; }));
    window.addEventListener('pageshow', () => { const loader = document.querySelector('.admin-loading'); if (loader) loader.hidden = true; });
})();
