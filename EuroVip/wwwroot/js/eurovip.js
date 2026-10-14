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

// Buscador global de la barra de arriba (Ctrl+K): pantallas, circuitos, hoteles, ofertas y catálogos.
(function () {
    const caja = document.querySelector('.ev-search');
    const input = caja?.querySelector('input');
    if (!input) return;
    input.setAttribute('aria-label', 'Buscar en Eurovip');
    input.setAttribute('autocomplete', 'off');
    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-expanded', 'false');
    const lista = document.createElement('div');
    lista.className = 'ev-busq'; lista.id = 'evBusq'; lista.hidden = true; lista.setAttribute('role', 'listbox');
    input.setAttribute('aria-controls', 'evBusq');
    caja.appendChild(lista);
    const esc = s => (s ?? '').toString().replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    let act = -1, ultimo = '', timer = null, ctrl = null;

    const abrir = v => { lista.hidden = !v; input.setAttribute('aria-expanded', String(v)); };
    const items = () => [...lista.querySelectorAll('a[data-i]')];
    const marcar = i => { const l = items(); act = l.length ? (i + l.length) % l.length : -1; l.forEach((a, k) => a.classList.toggle('act', k === act)); l[act]?.scrollIntoView({ block: 'nearest' }); };

    async function buscar() {
        const q = input.value.trim();
        if (q === ultimo) return;
        ultimo = q;
        if (q.length < 2) { lista.innerHTML = q ? '<div class="vacio">Escribe al menos 2 letras…</div>' : ''; abrir(!!q); return; }
        lista.innerHTML = '<div class="vacio"><span class="ev-busq-spin"></span> Buscando…</div>'; abrir(true);
        ctrl?.abort(); ctrl = new AbortController();
        try {
            const r = await fetch('/Home/Buscar?q=' + encodeURIComponent(q), { headers: { Accept: 'application/json' }, signal: ctrl.signal });
            const l = r.ok ? await r.json() : [];
            if (input.value.trim() !== q) return;
            if (!l.length) { lista.innerHTML = `<div class="vacio">No encontramos nada con «${esc(q)}».</div>`; act = -1; return; }
            let grupo = '', i = 0;
            lista.innerHTML = l.map(x => {
                const g = x.grupo !== grupo ? `<div class="g">${esc(x.grupo)}</div>` : ''; grupo = x.grupo;
                return g + `<a href="${esc(x.url)}" data-i="${i++}" role="option"><i class="bi ${esc(x.icono)}"></i><span><b>${esc(x.titulo)}</b>${x.sub ? `<small>${esc(x.sub)}</small>` : ''}</span></a>`;
            }).join('');
            marcar(0);
        } catch (e) { if (e.name !== 'AbortError') lista.innerHTML = '<div class="vacio">No se pudo buscar. Intenta de nuevo.</div>'; }
    }

    input.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(buscar, 220); });
    input.addEventListener('focus', () => { if (lista.innerHTML) abrir(true); });
    input.addEventListener('keydown', e => {
        if (e.key === 'ArrowDown') { e.preventDefault(); marcar(act + 1); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); marcar(act - 1); }
        else if (e.key === 'Enter') { const a = items()[act]; if (a) { e.preventDefault(); location.href = a.href; } }
        else if (e.key === 'Escape') { e.stopPropagation(); abrir(false); input.blur(); }
    });
    document.addEventListener('click', e => { if (!caja.contains(e.target)) abrir(false); });
})();
