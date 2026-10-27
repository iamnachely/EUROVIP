// Quién y cuándo (FASE 33): debajo del subtítulo de un editor, «Creado por X · fecha» y «Último cambio: Y · fecha».
//   EVQuien.pintar(subtitulo, 'crucero', 12)   → pide /Quien/Ver y lo muestra (si es anterior al 27/10/2026, lo dice)
//   EVQuien.quitar(subtitulo)                  → para «nuevo» o «duplicar»
// qué: hotel, oferta, circuito, excursion, traslado, crucero. Sin dependencias (Hoteles no carga eurovip-ui.js).
window.EVQuien = (() => {
    const css = document.createElement('style');
    css.textContent = `.ev-quien { display: flex !important; flex-wrap: wrap; gap: 4px 12px; margin-top: 3px; font-size: 11.5px !important; font-weight: 600; color: var(--ev-muted, #66807C); }
.ev-quien b { color: var(--ev-text, #12302D); font-weight: 800; }`;
    document.head.appendChild(css);
    const esc = s => (s ?? '').toString().replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    // sin hora (solo la fecha, como servicio.apertura) no se muestra «00:00»
    const cuando = v => {
        if (!v) return '';
        const d = new Date(v), conHora = d.getHours() + d.getMinutes() + d.getSeconds() > 0;
        return d.toLocaleString('es', { day: '2-digit', month: 'short', year: 'numeric', ...(conHora ? { hour: '2-digit', minute: '2-digit' } : {}) });
    };
    let turno = 0;

    function quitar(sub) { sub?.parentElement?.querySelector(':scope > .ev-quien')?.remove(); turno++; }

    async function pintar(sub, que, id) {
        quitar(sub);
        if (!sub || id == null || id === '') return;
        const mio = turno;
        let r;
        try {
            const resp = await fetch(`/Quien/Ver?que=${encodeURIComponent(que)}&id=${encodeURIComponent(id)}`, { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
            if (!resp.ok) return;
            r = await resp.json();
        } catch { return; }
        if (mio !== turno) return;   // ya se abrió otro registro
        const p = [];
        // con usuario: «Creado por X · fecha»; solo con fecha (registros anteriores): «Creado el …»
        if (r.usuario_Alta || r.fecha_Alta)
            p.push(`<span><i class="bi bi-plus-circle"></i> ${r.usuario_Alta ? `Creado por <b>${esc(r.usuario_Alta)}</b>` : 'Creado'}${r.fecha_Alta ? (r.usuario_Alta ? ' · ' : ' el ') + cuando(r.fecha_Alta) : ''}</span>`);
        if (r.usuario_Modificacion || r.fecha_Modificacion)
            p.push(`<span><i class="bi bi-pencil"></i> Último cambio${r.usuario_Modificacion ? `: <b>${esc(r.usuario_Modificacion)}</b>` : ''}${r.fecha_Modificacion ? (r.usuario_Modificacion ? ' · ' : ' el ') + cuando(r.fecha_Modificacion) : ''}</span>`);
        const el = document.createElement('small');
        el.className = 'ev-quien';
        el.innerHTML = p.join('') || '<span><i class="bi bi-clock-history"></i> Sin registro de quién lo creó (anterior al 27/10/2026)</span>';
        sub.insertAdjacentElement('afterend', el);
    }

    return { pintar, quitar };
})();
