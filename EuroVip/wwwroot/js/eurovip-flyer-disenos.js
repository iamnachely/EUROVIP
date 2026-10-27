// Diseños modernos del flyer + colores a elegir + moneda (Flyer Agencia y Flyer Editor). Estilos: css/eurovip-flyer-disenos.css.
//   EVDisenos.LISTA                         → [{ id, nombre, mini }] de los 6 diseños modernos
//   EVDisenos.es(id)                        → true si es uno de ellos
//   EVDisenos.aplicar(flyer, id)            → pone las clases y los colores guardados (id no moderno = los quita)
//   EVDisenos.panelColores(caja, id, alCambiar) → los 7 selectores de color + «Restablecer»
//   EVDisenos.panelMoneda(caja, alCambiar)  → símbolo (RD$, US$, €, $) y posición (automática, delante, detrás)
//   EVDisenos.precio(n)                     → «RD$ 12.990» o «12.990 €» según la moneda elegida
//   EVDisenos.aplicarMoneda(raiz)           → cambia y coloca el símbolo en los precios del flyer del Editor (.precio-sym / .precio-val)
// Colores y moneda se guardan en este navegador (los comparten Flyer Agencia y Flyer Editor).
window.EVDisenos = (() => {
    const LISTA = [
        { id: 'neon', nombre: 'Neón', mini: 'linear-gradient(135deg,#0B0B1A 55%,#00F0FF 56%,#FF3DF2)' },
        { id: 'cristal', nombre: 'Cristal', mini: 'linear-gradient(160deg,#0E3B4A,#7FE7DC)' },
        { id: 'bento', nombre: 'Bento', mini: 'conic-gradient(#FF5A36 0 25%,#fff 0 50%,#F2F0EB 0 75%,#fff 0)' },
        { id: 'bloque', nombre: 'Bloque', mini: 'linear-gradient(180deg,#1F2BFF 70%,#E8FF3A 70%)' },
        { id: 'editorial', nombre: 'Editorial', mini: 'linear-gradient(180deg,#FBF8F2 40%,#1B1B1B 40% 43%,#FBF8F2 43%)' },
        { id: 'aurora', nombre: 'Aurora', mini: 'radial-gradient(circle at 0 0,#C7D2FE,transparent 60%),radial-gradient(circle at 100% 100%,#A7F3D0,transparent 60%),#FFE3EC' },
    ];
    const VARS = [['--fx-titulo', 'Título'], ['--fx-fondo', 'Fondo'], ['--fx-tarjeta', 'Tarjetas'], ['--fx-texto', 'Letras'], ['--fx-suave', 'Texto 2.º'],
                  ['--fx-num', 'Números'], ['--fx-precio', 'Precios'], ['--fx-acento', 'Acento']];
    const MONEDAS = [['RD$', 'RD$ · Peso dominicano'], ['US$', 'US$ · Dólar'], ['€', '€ · Euro'], ['$', '$']];
    const es = id => LISTA.some(d => d.id === id);
    const leer = (k, d) => { try { return JSON.parse(localStorage.getItem(k)) ?? d; } catch { return d; } };
    const guardar = (k, v) => { try { localStorage.setItem(k, JSON.stringify(v)); } catch { } };
    const esc = s => (s ?? '').toString().replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    // ---- colores ----
    const todos = () => leer('ev-flyer-colores', {});
    const propios = id => todos()[id] || {};
    /** Colores de fábrica del diseño (los lee del CSS con un flyer de prueba invisible). */
    function deFabrica(id) {
        const p = document.createElement('div');
        p.className = `flyer fx fx-${id}`; p.style.cssText = 'position:absolute;left:-9999px;top:0;width:10px;height:10px';
        document.body.appendChild(p);
        const cs = getComputedStyle(p), r = Object.fromEntries(VARS.map(([v]) => [v, cs.getPropertyValue(v).trim() || (v === '--fx-titulo' ? '#ffffff' : '')]));
        p.remove();
        return r;
    }
    const ctx = document.createElement('canvas').getContext('2d');
    /** Cualquier color CSS → #rrggbb (para el selector de color; la transparencia no se puede elegir ahí). */
    function aHex(c) {
        if (!c) return '#000000';
        ctx.fillStyle = '#000'; ctx.fillStyle = c;
        const v = ctx.fillStyle;
        if (v.startsWith('#')) return v;
        const m = v.match(/[\d.]+/g); if (!m) return '#000000';
        return '#' + m.slice(0, 3).map(n => (+n).toString(16).padStart(2, '0')).join('');
    }

    function aplicar(fl, id) {
        if (!fl) return;
        fl.classList.remove('fx', ...LISTA.map(d => 'fx-' + d.id));
        VARS.forEach(([v]) => fl.style.removeProperty(v));
        if (!es(id)) return;
        fl.classList.add('fx', 'fx-' + id);
        Object.entries(propios(id)).forEach(([v, c]) => fl.style.setProperty(v, c));
    }

    function panelColores(caja, id, alCambiar) {
        if (!caja) return;
        if (!es(id)) { caja.innerHTML = ''; caja.hidden = true; return; }
        caja.hidden = false;
        const fab = deFabrica(id), mios = propios(id);
        caja.innerHTML = `<div class="evd-colores">${VARS.map(([v, n]) => `<label title="${esc(n)}"><input type="color" data-var="${v}" value="${aHex(mios[v] || fab[v])}"><span>${esc(n)}</span></label>`).join('')}
            <button type="button" class="evd-rest" title="Volver a los colores del diseño"><i class="bi bi-arrow-counterclockwise"></i> Restablecer</button></div>`;
        caja.querySelectorAll('input[type=color]').forEach(inp => inp.addEventListener('input', () => {
            const t = todos(); (t[id] ??= {})[inp.dataset.var] = inp.value; guardar('ev-flyer-colores', t); alCambiar?.();
        }));
        caja.querySelector('.evd-rest').onclick = () => { const t = todos(); delete t[id]; guardar('ev-flyer-colores', t); panelColores(caja, id, alCambiar); alCambiar?.(); };
    }

    // ---- moneda ----
    const moneda = () => ({ sym: 'RD$', pos: 'auto', ...leer('ev-flyer-moneda', {}) });
    /** El euro va detrás («12.990 €»); RD$, US$ y $ delante. «Delante» / «Detrás» lo fuerzan. */
    const detras = (m = moneda()) => m.pos === 'detras' || (m.pos === 'auto' && m.sym === '€');
    const numero = n => Number(n).toLocaleString('es-ES', { maximumFractionDigits: 0 });
    function precio(n) {
        if (n == null || n === '' || !isFinite(+n)) return '—';
        const m = moneda();
        return detras(m) ? `${numero(n)} ${m.sym}` : `${m.sym} ${numero(n)}`;
    }
    function aplicarMoneda(raiz) {
        if (!raiz) return;
        const m = moneda(), atras = detras(m), txt = atras ? ' ' + m.sym : m.sym + ' ';
        raiz.querySelectorAll('.fh-price').forEach(p => {
            const s = p.querySelector('.precio-sym'), v = p.querySelector('.precio-val');
            if (!s || !v) return;
            if (s.textContent !== txt) s.textContent = txt;
            const bien = atras ? v.nextElementSibling === s : s.nextElementSibling === v;
            if (!bien) { if (atras) v.after(s); else v.before(s); }
            p.classList.toggle('sym-detras', atras);
        });
    }
    function panelMoneda(caja, alCambiar) {
        if (!caja) return;
        const m = moneda();
        caja.innerHTML = `<span class="evd-mon"><i class="bi bi-currency-exchange"></i>
            <select data-m="sym" title="Símbolo de la moneda">${MONEDAS.map(([v, t]) => `<option value="${esc(v)}" ${v === m.sym ? 'selected' : ''}>${esc(t)}</option>`).join('')}</select>
            <select data-m="pos" title="Dónde va el símbolo"><option value="auto" ${m.pos === 'auto' ? 'selected' : ''}>Posición automática</option>
                <option value="delante" ${m.pos === 'delante' ? 'selected' : ''}>Delante (RD$ 100)</option><option value="detras" ${m.pos === 'detras' ? 'selected' : ''}>Detrás (100 €)</option></select></span>`;
        caja.querySelectorAll('select').forEach(s => s.addEventListener('change', () => {
            const n = moneda(); n[s.dataset.m] = s.value; guardar('ev-flyer-moneda', n); alCambiar?.();
        }));
    }

    const css = document.createElement('style');
    css.textContent = `.evd-colores { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.evd-colores label { display: inline-flex; align-items: center; gap: 6px; font-size: .78rem; font-weight: 700; color: #334155; background: #f8fafc; border: 1.5px solid #e2e8f0; border-radius: 9px; padding: 4px 8px 4px 4px; cursor: pointer; }
.evd-colores input[type=color] { width: 28px; height: 24px; border: 0; padding: 0; background: none; cursor: pointer; }
.evd-rest { border: 1.5px solid #e2e8f0; border-radius: 9px; background: #fff; padding: 5px 10px; font-size: .78rem; font-weight: 700; cursor: pointer; color: #334155; }
.evd-mon { display: inline-flex; align-items: center; gap: 6px; font-size: .82rem; font-weight: 700; color: #334155; }
.evd-mon select { border: 1.5px solid #e2e8f0; border-radius: 8px; padding: 5px 8px; font-weight: 700; font-size: .8rem; font-family: inherit; background: #f8fafc; color: #334155; }`;
    document.head.appendChild(css);

    return { LISTA, es, aplicar, panelColores, panelMoneda, precio, aplicarMoneda, moneda, detras };
})();
