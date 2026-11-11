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

    // ---------- Origen de venta por defecto (pedido de la usuaria) ----------
    // «Origen = Santo Domingo, País origen = República Dominicana en todas las cargas: es lo más común. Que ya venga elegido,
    // pero que se pueda cambiar en el Tom Select.» EVCodigo.origen(selectPais, inputCiudad, { opcional }) :
    //   • pone DO en el país y Santo Domingo en la ciudad;
    //   • convierte la ciudad en un Tom Select que busca en Eurovip.destino del país elegido (/Circuitos/Destinos);
    //   • el <input> de siempre sigue siendo el que manda (value y dataset.valor), así la pantalla no cambia nada más.
    const ORIGEN = { pais: 'DO', ciudad: 'Santo Domingo' };
    function origen(selPais, inp, { opcional = false } = {}) {
        if (!selPais || !inp) return;
        if ([...selPais.options].some(o => o.value === ORIGEN.pais)) selPais.value = ORIGEN.pais;
        const poner = v => {
            inp.value = v || '';
            inp.dispatchEvent(new Event('input', { bubbles: true }));   // la pantalla limpia dataset.valor y recalcula
            inp.dataset.valor = v || '';
            inp.dispatchEvent(new Event('change', { bubbles: true }));
            if (v) inp.closest('.ui-f')?.classList.remove('err');
        };
        inp.value = ORIGEN.ciudad; inp.dataset.valor = ORIGEN.ciudad;
        if (!window.TomSelect || inp.dataset.evOrigen) return;
        inp.dataset.evOrigen = '1';
        const caja = inp.closest('.ui-picker-wrap') || inp;
        caja.hidden = true; caja.style.display = 'none';
        const sel = document.createElement('select');
        sel.setAttribute('data-no-buscar', '');   // este Tom Select lo maneja esta función (no eurovip-select)
        sel.innerHTML = `${opcional ? '<option value="">— Sin origen —</option>' : ''}<option value="${esc(ORIGEN.ciudad)}" selected>${esc(ORIGEN.ciudad)}</option>`;
        caja.after(sel);
        const ts = new TomSelect(sel, {
            valueField: 'valor', labelField: 'texto', searchField: ['texto'], maxOptions: 60, preload: 'focus', allowEmptyOption: opcional,
            placeholder: 'Busca la ciudad de origen…',
            load: (q, cb) => fetch(`/Circuitos/Destinos?pais=${encodeURIComponent(selPais.value)}&q=${encodeURIComponent(q)}`, { headers: { Accept: 'application/json' } })
                .then(r => r.json()).then(l => cb(l.map(x => ({ valor: x.valor, texto: x.texto })))).catch(() => cb()),
            render: { no_results: () => '<div class="no-results">No encontré esa ciudad en el país elegido.</div>' },
            onChange: v => poner(v)
        });
        // Otro país → se vacía la ciudad y se busca en el nuevo país
        selPais.addEventListener('change', () => {
            ts.clear(true); ts.clearOptions(); ts.loadedSearches = {}; ts.lastQuery = null;
            if (opcional) ts.addOption({ valor: '', texto: '— Sin origen —' });
            inp.value = ''; inp.dataset.valor = '';
        });
        inp.evOrigen = { poner: v => { if (v && !ts.options[v]) ts.addOption({ valor: v, texto: v }); ts.setValue(v || '', true); inp.value = v || ''; inp.dataset.valor = v || ''; } };
    }

    // ---------- Información para el cliente (CAMBIO 130) ----------
    // Los mismos 6 textos opcionales en todos los productos. EVCodigo.info(valores) → HTML del bloque (se abre solo si tiene algo);
    // EVCodigo.leerInfo(raiz) → { Incluye, No_Incluye, Condiciones, Documentacion, Importante, Oferta_Especial } para mandar al guardar.
    const INFO = [
        ['Incluye', 'Incluye', 'Una cosa por línea. Ej.: Traslado ida y vuelta · Almuerzo buffet'],
        ['No_Incluye', 'No incluye', 'Una cosa por línea. Ej.: Propinas · Bebidas premium'],
        ['Condiciones', 'Condiciones', 'Cancelación, cambios, edades, horarios…'],
        ['Documentacion', 'Documentación', 'Pasaporte, visado, vacunas…'],
        ['Importante', 'Importante', 'Lo que el cliente tiene que saber sí o sí (qué llevar, punto de encuentro…)'],
        ['Oferta_Especial', 'Oferta especial', 'Promoción o ventaja de esta oferta']
    ];
    let cssInfo = false;
    function info(v = {}, { titulo = 'Información para el cliente' } = {}) {
        if (!cssInfo) {
            cssInfo = true;
            const s = document.createElement('style');
            s.textContent = `.ev-info{border:1px solid var(--ev-line,#E2EFED);border-radius:16px;padding:0 14px;margin:12px 0;background:var(--ev-surface,#fff)}
.ev-info>summary{cursor:pointer;padding:12px 0;font-weight:800;list-style:none;display:flex;gap:8px;align-items:center}
.ev-info>summary::-webkit-details-marker{display:none}
.ev-info>summary em{font-style:normal;font-weight:600;color:var(--ev-muted,#66807C);font-size:12.5px}
.ev-info>summary .n{margin-left:auto;font-size:12px;font-weight:700;color:var(--ev-deep,#0E8F86)}
.ev-info .g{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:10px 14px;padding:0 0 14px}
.ev-info label{display:block;font-size:12.5px;font-weight:800;margin-bottom:4px}
.ev-info textarea{width:100%;min-height:74px;border:1px solid var(--ev-line,#E2EFED);border-radius:12px;padding:8px 10px;font:inherit;font-size:13.5px;background:var(--ev-surface,#fff);color:inherit;resize:vertical}`;
            document.head.appendChild(s);
        }
        const val = k => v?.[k] ?? v?.[k[0].toLowerCase() + k.slice(1)] ?? '';
        const llenos = INFO.filter(([k]) => String(val(k)).trim()).length;
        return `<details class="ev-info" data-ev-info data-siempre ${llenos ? 'open' : ''}><summary><i class="bi bi-info-circle"></i> ${esc(titulo)} <em>opcional · se ve en la web</em><span class="n">${llenos ? llenos + ' con texto' : 'vacío'}</span></summary>
            <div class="g">${INFO.map(([k, t, ph]) => `<div><label>${esc(t)}</label><textarea data-info="${k}" placeholder="${esc(ph)}">${esc(val(k))}</textarea></div>`).join('')}</div></details>`;
    }
    function leerInfo(raiz) {
        const b = raiz?.matches?.('[data-ev-info]') ? raiz : raiz?.querySelector('[data-ev-info]');
        if (!b) return null;
        return Object.fromEntries(INFO.map(([k]) => [k, b.querySelector(`[data-info="${k}"]`)?.value.trim() || null]));
    }

    return { campo, validar, proveedores, monedas, simbolo, limpiar, nombreProveedor, origen, ORIGEN, info, leerInfo };
})();
