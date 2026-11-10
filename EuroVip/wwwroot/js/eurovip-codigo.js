// =====================================================================
// EUROVIP — piezas comunes de las cargas (FASE 38), en todas las pantallas (lo carga _Layout):
//   EVCodigo.campo(input, { producto, clave: () => id, anterior: () => código guardado })
//       Código interno: mientras se escribe quita espacios, tildes y símbolos, y revisa en el servidor que
//       no lo use ningún otro producto (/Codigos/Disponible). Debajo dice «libre» o quién lo usa.
//       await EVCodigo.validar(input) → true si se puede guardar (vacío también vale: es opcional).
//   EVCodigo.proveedores(select, valor)  llena un <select> con todos los proveedores (buscador Tom Select).
//   EVCodigo.monedas(select, valor)      llena un <select> con las monedas (DOP por defecto).
//   EVCodigo.simbolo(moneda)             RD$ / US$ / €
// =====================================================================
window.EVCodigo = (() => {
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const simbolo = m => ({ EUR: '€', USD: 'US$', DOP: 'RD$' }[(m || 'DOP').trim().toUpperCase()] || m);
    const limpiar = t => (t || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^A-Za-z0-9]/g, '').slice(0, 30);

    const css = document.createElement('style');
    css.textContent = `.ev-cod-msg{display:flex;align-items:center;gap:6px;margin-top:5px;font:700 12px var(--ev-font,inherit);color:var(--ev-muted,#66807C);min-height:16px}
.ev-cod-msg.ok{color:#067647}.ev-cod-msg.mal{color:#B42318}.ev-cod-msg .sp{width:12px;height:12px;border:2px solid currentColor;border-right-color:transparent;border-radius:50%;animation:evcod 0.8s linear infinite}
@keyframes evcod{to{transform:rotate(360deg)}}
input.ev-cod-mal{border-color:#F04438 !important;background:#FFFBFA !important}input.ev-cod-ok{border-color:#12B76A !important}`;
    document.head.appendChild(css);

    function campo(input, o = {}) {
        if (!input || input.evCodigo) return input?.evCodigo;
        input.setAttribute('maxlength', '30');
        input.setAttribute('autocomplete', 'off');
        input.setAttribute('spellcheck', 'false');
        if (!input.placeholder) input.placeholder = 'Ej. PUJ2026A';
        const msg = document.createElement('small');
        msg.className = 'ev-cod-msg';
        input.insertAdjacentElement('afterend', msg);
        let ultimo = null, pendiente = null, t = null;

        const pintar = (clase, html) => {
            msg.className = 'ev-cod-msg ' + clase; msg.innerHTML = html;
            input.classList.toggle('ev-cod-mal', clase === 'mal'); input.classList.toggle('ev-cod-ok', clase === 'ok');
        };
        async function revisar() {
            const c = input.value.trim();
            if (!c) { pintar('', '<i class="bi bi-info-circle"></i> Opcional: letras y números, sin espacios.'); ultimo = { c, ok: true }; return true; }
            if (ultimo && ultimo.c === c) return ultimo.ok;
            pintar('', '<span class="sp"></span> Revisando que no exista…');
            const q = new URLSearchParams({ codigo: c, producto: o.producto || '', clave: o.clave?.() ?? '', anterior: o.anterior?.() ?? '' });
            try {
                const r = await fetch('/Codigos/Disponible?' + q, { headers: { Accept: 'application/json' } });
                const d = await r.json();
                if (input.value.trim() !== c) return revisar();   // siguió escribiendo
                ultimo = { c, ok: !!d.ok };
                pintar(d.ok ? 'ok' : 'mal', `<i class="bi ${d.ok ? 'bi-check-circle-fill' : 'bi-x-circle-fill'}"></i> ${esc(d.mensaje)}`);
                return ultimo.ok;
            } catch { pintar('mal', '<i class="bi bi-wifi-off"></i> No se pudo revisar el código. Intenta de nuevo.'); return false; }
        }
        input.addEventListener('input', () => {
            const antes = input.value, limpio = limpiar(antes);
            if (antes !== limpio) {
                const pos = input.selectionStart - (antes.length - limpio.length);
                input.value = limpio;
                try { input.setSelectionRange(Math.max(0, pos), Math.max(0, pos)); } catch { }
                pintar('mal', '<i class="bi bi-exclamation-circle"></i> Solo letras y números: se quitaron espacios, tildes o símbolos.');
            }
            clearTimeout(t); ultimo = null;
            t = setTimeout(() => { pendiente = revisar(); }, 350);
        });
        // Al abrir un formulario con otro valor (editar) se vuelve a revisar
        const h = { revisar: () => (pendiente = revisar()), reiniciar() { ultimo = null; msg.className = 'ev-cod-msg'; msg.textContent = ''; input.classList.remove('ev-cod-mal', 'ev-cod-ok'); } };
        input.evCodigo = h;
        return h;
    }
    async function validar(input) {
        if (!input?.evCodigo) return true;
        return await input.evCodigo.revisar();
    }

    let cacheProv = null, cacheMon = null;
    async function json(url) { const r = await fetch(url, { headers: { Accept: 'application/json' } }); if (!r.ok) throw new Error('No se pudo cargar ' + url); return r.json(); }
    async function proveedores(select, valor, { vacio = '— Sin proveedor —' } = {}) {
        cacheProv ??= json('/Codigos/Proveedores');
        const l = await cacheProv;
        const grupos = {};
        l.forEach(p => (grupos[p.extra || 'Otros'] ??= []).push(p));
        select.innerHTML = `<option value="">${esc(vacio)}</option>` + Object.entries(grupos).map(([g, ps]) =>
            `<optgroup label="${esc(g)}">${ps.map(p => `<option value="${esc(p.valor)}">${esc(p.texto)}</option>`).join('')}</optgroup>`).join('');
        select.value = valor ?? '';
        if (valor && select.value !== String(valor)) { select.add(new Option(valor, valor, true, true)); }
        return l;
    }
    async function monedas(select, valor) {
        cacheMon ??= json('/Codigos/Monedas');
        const l = await cacheMon;
        select.innerHTML = l.map(m => `<option value="${esc(m.valor)}">${esc(m.valor)} · ${esc(simbolo(m.valor))} ${esc(m.texto)}</option>`).join('');
        select.value = (valor || 'DOP').trim().toUpperCase();
        if (!select.value) select.value = 'DOP';
        return l;
    }
    const nombreProveedor = async id => { if (!id) return ''; cacheProv ??= json('/Codigos/Proveedores'); return (await cacheProv).find(p => p.valor === id)?.texto || id; };

    return { campo, validar, proveedores, monedas, simbolo, limpiar, nombreProveedor };
})();
