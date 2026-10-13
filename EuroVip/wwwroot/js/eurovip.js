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

// Menú lateral: grupos plegables (recuerda cómo los dejó el usuario) y opción activa siempre a la vista
(function () {
    const clave = 'ev-menu-cerrados';
    let cerrados = [];
    try { cerrados = JSON.parse(localStorage.getItem(clave) || '[]'); } catch (e) { }
    const guardar = () => { try { localStorage.setItem(clave, JSON.stringify(cerrados)); } catch (e) { } };

    document.querySelectorAll('.ev-grupo').forEach(g => {
        const id = g.dataset.grupo, btn = g.querySelector('.ev-nav-label');
        const tieneActivo = !!g.querySelector('a.active');
        const fijar = cerrar => { g.classList.toggle('cerrado', cerrar); btn.setAttribute('aria-expanded', String(!cerrar)); };
        fijar(cerrados.includes(id) && !tieneActivo);
        btn.addEventListener('click', () => {
            const cerrar = !g.classList.contains('cerrado');
            g.classList.add('animando');
            fijar(cerrar);
            setTimeout(() => g.classList.remove('animando'), 320);
            cerrados = cerrados.filter(x => x !== id);
            if (cerrar) cerrados.push(id);
            guardar();
        });
    });

    document.querySelectorAll('.ev-nav a.pronto').forEach(a => {
        a.title = 'Próximamente';
        a.addEventListener('click', e => e.preventDefault());
    });

    const activo = document.querySelector('.ev-sidebar-scroll a.active');
    if (activo) activo.scrollIntoView({ block: 'nearest' });
})();
