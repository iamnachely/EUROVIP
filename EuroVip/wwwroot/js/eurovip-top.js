// =====================================================================
// EUROVIP — barra superior: campana de notificaciones y menú del usuario
// (va en el layout; no depende de eurovip-ui.js)
// =====================================================================
(function () {
    const $ = id => document.getElementById(id);
    const bell = $('evBell'), panel = $('evNotif'), user = $('evUser'), menu = $('evUserMenu');
    if (!bell || !user) return;

    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const post = url => fetch(url, { method: 'POST', headers: { 'RequestVerificationToken': token() } });

    // ---------- abrir / cerrar ----------
    const pares = [[bell, panel], [user, menu]];
    const cerrar = (excepto) => pares.forEach(([b, p]) => { if (p !== excepto) { p.hidden = true; b.setAttribute('aria-expanded', 'false'); } });
    pares.forEach(([b, p]) => b.addEventListener('click', e => {
        e.stopPropagation();
        const abrir = p.hidden;
        cerrar(p);
        p.hidden = !abrir;
        b.setAttribute('aria-expanded', String(abrir));
        if (abrir && p === panel) cargar();
    }));
    document.addEventListener('click', e => { if (!e.target.closest('.ev-pop')) cerrar(); });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') cerrar(); });

    // ---------- notificaciones ----------
    let datos = { items: [], noLeidas: 0 }, filtro = '', ultimoNoLeidas = null;

    const dia = f => {
        const d = new Date(f), hoy = new Date(); hoy.setHours(0, 0, 0, 0);
        const dd = new Date(d); dd.setHours(0, 0, 0, 0);
        const diff = Math.round((hoy - dd) / 86400000);
        return diff === 0 ? 'Hoy' : diff === 1 ? 'Ayer' : d.toLocaleDateString('es-ES', { weekday: 'long', day: 'numeric', month: 'long' });
    };
    const hace = f => {
        const s = (Date.now() - new Date(f)) / 1000;
        if (s < 60) return 'ahora';
        if (s < 3600) return `hace ${Math.round(s / 60)} min`;
        if (s < 86400) return `hace ${Math.round(s / 3600)} h`;
        return new Date(f).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' });
    };

    function badge() {
        const n = datos.noLeidas, b = $('evBellN');
        b.hidden = n === 0;
        b.textContent = n > 9 ? '9+' : n;
        bell.setAttribute('aria-label', n ? `Notificaciones: ${n} sin leer` : 'Notificaciones');
        $('evNotifLeer').disabled = n === 0;
        if (ultimoNoLeidas !== null && n > ultimoNoLeidas) { bell.classList.remove('sonando'); void bell.offsetWidth; bell.classList.add('sonando'); }
        ultimoNoLeidas = n;
    }

    function pintar() {
        const items = datos.items.filter(n => !filtro || !n.leida);
        if (!items.length) {
            $('evNotifLista').innerHTML = `<div class="ev-notif-vacio"><i class="bi ${filtro ? 'bi-check2-circle' : 'bi-bell-slash'}"></i>${filtro ? '¡Todo leído! No tienes avisos pendientes.' : 'Todavía no hay avisos.'}</div>`;
            return;
        }
        let html = '', grupo = '';
        for (const n of items) {
            const g = dia(n.fecha);
            if (g !== grupo) { html += `<div class="ev-notif-dia">${esc(g)}</div>`; grupo = g; }
            html += `<a class="ev-notif-it ${esc(n.tipo)} ${n.leida ? '' : 'nueva'}" href="${esc(n.url)}">
                <span class="ni"><i class="bi ${esc(n.icono)}"></i></span>
                <span style="min-width:0;padding-right:14px"><b>${esc(n.titulo)}</b>${n.texto ? `<span class="tx">${esc(n.texto)}</span>` : ''}<small>${hace(n.fecha)}</small></span></a>`;
        }
        $('evNotifLista').innerHTML = html;
    }

    async function cargar() {
        try {
            const r = await fetch('/Perfil/Notificaciones', { headers: { 'Accept': 'application/json' } });
            if (!r.ok) return;
            datos = await r.json();
            badge();
            if (!panel.hidden) pintar();
        } catch { /* sin conexión: se reintenta en el próximo ciclo */ }
    }

    $('evNotifFiltro').addEventListener('click', e => {
        const b = e.target.closest('[data-f]'); if (!b) return;
        e.stopPropagation();
        filtro = b.dataset.f;
        [...$('evNotifFiltro').children].forEach(x => x.classList.toggle('on', x === b));
        pintar();
    });
    $('evNotifLeer').addEventListener('click', async e => {
        e.stopPropagation();
        const r = await post('/Perfil/MarcarLeidas');
        if (r.ok) { datos.items.forEach(n => n.leida = true); datos.noLeidas = 0; badge(); pintar(); }
    });

    cargar();
    setInterval(() => { if (!document.hidden) cargar(); }, 120000);

    // ---------- foto y nombre al día (por si la sesión se reinició) ----------
    fetch('/Perfil/Datos', { headers: { 'Accept': 'application/json' } }).then(r => r.ok ? r.json() : null).then(p => {
        if (!p) return;
        if (p.foto) ['evAvatar', 'evAvatarXl'].forEach(id => { const a = $(id); if (a && !a.querySelector('img')) a.innerHTML = `<img src="${esc(p.foto)}" alt="">`; });
    }).catch(() => { });
})();
