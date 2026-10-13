// Eurovip — comportamiento del shell (tema claro/oscuro, atajo de búsqueda, cerrar menú móvil)
(function () {
    const root = document.documentElement;
    const btn = document.getElementById('evTheme');

    function pintarIcono() {
        if (!btn) return;
        btn.innerHTML = root.dataset.theme === 'dark' ? '<i class="bi bi-sun"></i>' : '<i class="bi bi-moon-stars"></i>';
    }

    btn?.addEventListener('click', () => {
        root.dataset.theme = root.dataset.theme === 'dark' ? 'light' : 'dark';
        try { localStorage.setItem('ev-theme', root.dataset.theme); } catch (e) { }
        pintarIcono();
    });
    pintarIcono();

    document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
            e.preventDefault();
            document.querySelector('.ev-search input')?.focus();
        }
        if (e.key === 'Escape') document.body.classList.remove('ev-menu-open');
    });
})();
