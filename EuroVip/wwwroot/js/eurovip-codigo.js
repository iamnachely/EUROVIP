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
    // Pedido de la usuaria: «que sea simple, opcional, y que cada sección esté en una línea». Cada texto va en SU fila:
    // etiqueta + ayuda corta a la izquierda, campo a la derecha (crece solo al escribir). Arriba, un contador «2 de 6».
    //   EVCodigo.info(valores, { titulo, plano, ids, attrs })
    //      plano: true  → sin desplegable (para ponerlo dentro de un tab «Información para el cliente»)
    //      ids:   { Incluye: 'nIncluye', … } → id de cada campo (pantallas que ya leen sus campos por id)
    //      attrs: { Incluye: 'data-k="incluye"', … } → atributos extra de cada campo (texto fijo del código, no del usuario)
    //   EVCodigo.contarInfo(raiz) → cuántos de los 6 tienen texto · EVCodigo.infoContador(elemento, raiz) pinta ese número en vivo
    //   (en un tab, por ejemplo). Los nombres que se mandan al servidor NO cambian: data-info="Incluye" … "Oferta_Especial".
    const INFO = [
        ['Incluye', 'Incluye', 'Lo que entra en el precio', 'Ej.: Traslado ida y vuelta, desayuno buffet (una cosa por línea)', 'bi-check2-circle'],
        ['No_Incluye', 'No incluye', 'Lo que se paga aparte', 'Ej.: Propinas, bebidas premium, tasas locales', 'bi-x-circle'],
        ['Condiciones', 'Condiciones', 'Cancelación, cambios, edades', 'Ej.: Cancelación gratis hasta 7 días antes de la salida', 'bi-file-text'],
        ['Documentacion', 'Documentación', 'Papeles para viajar', 'Ej.: Pasaporte con 6 meses de vigencia', 'bi-passport'],
        ['Importante', 'Importante', 'Lo que debe saber sí o sí', 'Ej.: Presentarse 30 minutos antes en el lobby del hotel', 'bi-exclamation-triangle'],
        ['Oferta_Especial', 'Oferta especial', 'Promoción o ventaja', 'Ej.: 2do niño gratis reservando antes del 30 de junio', 'bi-stars']
    ];
    let cssInfo = false;
    function cssDeInfo() {
        if (cssInfo) return;
        cssInfo = true;
        const s = document.createElement('style');
        s.textContent = `.ev-info{border:1px solid var(--ev-line,#E2EFED);border-radius:16px;margin:12px 0;background:var(--ev-surface,#fff)}
.ev-info.plano{border:0;margin:0;background:none}
.ev-info>summary,.ev-info>.cab{padding:12px 14px;font-weight:800;display:flex;gap:8px;align-items:center;flex-wrap:wrap}
.ev-info>summary{cursor:pointer;list-style:none;border-radius:16px}
.ev-info>summary:hover{background:var(--ev-surface-2,#F4F8F7)}
.ev-info.plano>.cab{padding:0 0 10px}
.ev-info>summary::-webkit-details-marker{display:none}
.ev-info>summary em,.ev-info>.cab em{font-style:normal;font-weight:600;color:var(--ev-muted,#66807C);font-size:12.5px}
.ev-info .opc{font-size:11px;font-weight:800;padding:2px 9px;border-radius:99px;background:var(--ev-surface-2,#F4F8F7);border:1px solid var(--ev-line,#E2EFED);color:var(--ev-muted,#66807C);text-transform:uppercase;letter-spacing:.3px}
.ev-info .n{margin-left:auto;font-size:12px;font-weight:800;color:var(--ev-muted,#66807C);background:var(--ev-surface-2,#F4F8F7);padding:3px 10px;border-radius:99px}
.ev-info .n.si{background:var(--ev-ink,#0B1215);color:var(--ev-aqua,#74F2E6)}
.ev-info>summary .chev{transition:transform .2s;color:var(--ev-muted,#66807C)}
.ev-info[open]>summary .chev{transform:rotate(180deg)}
.ev-info .ayu{margin:0 14px 10px;font-size:12.5px;font-weight:600;color:var(--ev-muted,#66807C)}
.ev-info.plano .ayu{margin:0 0 12px}
.ev-info .filas{display:flex;flex-direction:column;gap:8px;padding:0 14px 14px}
.ev-info.plano .filas{padding:0}
.ev-info .fila{display:grid;grid-template-columns:200px minmax(0,1fr);gap:12px;align-items:start;padding:8px 10px;border-radius:14px;background:var(--ev-surface-2,#F4F8F7);border:1.5px solid transparent;transition:border-color .2s,background .2s}
.ev-info .fila:focus-within{border-color:var(--ev-teal,#2BD4C4);background:var(--ev-surface,#fff)}
.ev-info .fila>label{display:flex;gap:9px;align-items:flex-start;padding-top:7px;margin:0;font-size:13px;font-weight:800;cursor:pointer;line-height:1.25}
.ev-info .fila>label>i{font-size:16px;color:var(--ev-muted,#66807C);margin-top:-1px}
.ev-info .fila.llena>label>i{color:var(--ev-deep,#0E8F86)}
.ev-info .fila>label small{display:block;font-size:11.5px;font-weight:600;color:var(--ev-muted,#66807C);margin-top:2px}
.ev-info textarea{width:100%;min-height:40px;border:1.5px solid var(--ev-line,#E2EFED);border-radius:11px;padding:8px 11px;font:500 14px var(--ev-font,inherit);line-height:1.45;background:var(--ev-surface,#fff);color:var(--ev-text,inherit);resize:vertical;outline:0;overflow:hidden}
.ev-info textarea:focus{border-color:var(--ev-teal,#2BD4C4);box-shadow:0 0 0 4px rgba(116,242,230,.3)}
.ev-info textarea::placeholder{color:var(--ev-muted,#66807C);opacity:.75}
@media (max-width:640px){.ev-info .fila{grid-template-columns:1fr;gap:4px}.ev-info .fila>label{padding-top:2px}}`;
        document.head.appendChild(s);
    }
    // Filas visibles al pintar (1 si está vacío; crece con el texto, hasta 8)
    const filasDe = t => Math.min(8, Math.max(1, String(t).split('\n').reduce((n, l) => n + Math.max(1, Math.ceil(l.length / 70)), 0)));
    const crecer = ta => { ta.style.height = 'auto'; ta.style.height = Math.min(ta.scrollHeight + 3, 260) + 'px'; ta.style.overflowY = ta.scrollHeight > 257 ? 'auto' : 'hidden'; };
    const textoN = n => n ? `${n} de ${INFO.length} con texto` : 'vacío';
    function info(v = {}, { titulo = 'Información para el cliente', plano = false, ids = {}, attrs = {} } = {}) {
        cssDeInfo();
        const val = k => v?.[k] ?? v?.[k[0].toLowerCase() + k.slice(1)] ?? '';
        const llenos = INFO.filter(([k]) => String(val(k)).trim()).length;
        const cuenta = `<span class="n ${llenos ? 'si' : ''}" data-ev-info-n>${textoN(llenos)}</span>`;
        // titulo vacío = la sección de la pantalla ya tiene su propio título: solo «se ve en la web» y el contador
        const cab = titulo ? `<i class="bi bi-info-circle"></i> ${esc(titulo)} <span class="opc">Opcional</span> <em>se ve en la web</em>${cuenta}` : `<em><i class="bi bi-globe2"></i> Se ve en la web, en la ficha del producto.</em>${cuenta}`;
        const filas = INFO.map(([k, t, ayuda, ph, ico]) => {
            const id = ids[k] || `evi_${k}_${Math.random().toString(36).slice(2, 8)}`;
            return `<div class="fila ${String(val(k)).trim() ? 'llena' : ''}"><label for="${esc(id)}"><i class="bi ${ico}"></i><span>${esc(t)}<small>${esc(ayuda)}</small></span></label>`
                + `<textarea id="${esc(id)}" data-info="${k}" ${attrs[k] || ''} rows="${filasDe(val(k))}" placeholder="${esc(ph)}">${esc(val(k))}</textarea></div>`;
        }).join('');
        const cuerpo = `<p class="ayu">Nada aquí es obligatorio: escribe solo lo que quieras que el cliente lea. En «Incluye» y «No incluye» pon una cosa por línea (Enter).</p><div class="filas">${filas}</div>`;
        return plano
            ? `<div class="ev-info plano" data-ev-info data-siempre><div class="cab">${cab}</div>${cuerpo}</div>`
            : `<details class="ev-info" data-ev-info data-siempre ${llenos ? 'open' : ''}><summary>${cab}<i class="bi bi-chevron-down chev"></i></summary>${cuerpo}</details>`;
    }
    const bloqueInfo = raiz => raiz?.matches?.('[data-ev-info]') ? raiz : raiz?.querySelector?.('[data-ev-info]');
    function contarInfo(raiz) {
        const b = bloqueInfo(raiz);
        return b ? [...b.querySelectorAll('[data-info]')].filter(t => t.value.trim()).length : 0;
    }
    // Repinta el contador y las filas con texto (se llama solo al escribir; también se puede llamar tras poner valores por código)
    function refrescarInfo(raiz) {
        const b = bloqueInfo(raiz);
        if (!b) return 0;
        b.querySelectorAll('[data-info]').forEach(t => t.closest('.fila')?.classList.toggle('llena', !!t.value.trim()));
        const n = contarInfo(b), el = b.querySelector('[data-ev-info-n]');
        if (el) { el.textContent = textoN(n); el.classList.toggle('si', n > 0); }
        b.dispatchEvent(new CustomEvent('ev:info', { bubbles: true, detail: { llenos: n } }));
        return n;
    }
    document.addEventListener('input', e => {
        const t = e.target;
        if (!t?.matches?.('[data-ev-info] textarea[data-info]')) return;
        crecer(t); refrescarInfo(t.closest('[data-ev-info]'));
    });
    document.addEventListener('focusin', e => { if (e.target?.matches?.('[data-ev-info] textarea[data-info]')) crecer(e.target); });
    // Contador para un tab o un título: «Información para el cliente (2)». Sigue al bloque aunque se vuelva a pintar.
    function infoContador(el, raiz) {
        if (!el || !raiz) return;
        const pinta = () => { const n = contarInfo(raiz); el.textContent = n ? `${n}/${INFO.length}` : 'opcional'; el.classList.toggle('si', n > 0); };
        raiz.addEventListener('ev:info', pinta);
        new MutationObserver(pinta).observe(raiz, { childList: true, subtree: true });
        pinta();
        return pinta;
    }
    // ---------- Secciones plegables de las cargas (<details class="ui-pleg">) ----------
    // • Si un campo de adentro se marca con error, la sección se abre sola (para que se vea qué falta).
    // • EVCodigo.abrirConDatos(raiz): al editar, abre las secciones plegables que ya tienen algo cargado.
    const conDato = x => x.type === 'checkbox' || x.type === 'radio' ? false : x.tagName === 'SELECT' ? x.value !== '' && x.selectedIndex > 0 : (x.type !== 'hidden' && x.value.trim() !== '');
    function abrirConDatos(raiz = document) {
        raiz.querySelectorAll('details.ui-pleg, details[data-pleg]').forEach(d => { if ([...d.querySelectorAll('input, select, textarea')].some(conDato)) d.open = true; });
    }
    const vigilarErrores = () => new MutationObserver(ms => ms.forEach(m => {
        const t = m.target;
        if (t.classList?.contains('err') || t.classList?.contains('ev-cod-mal')) { const d = t.closest('details.ui-pleg:not([open]), details[data-pleg]:not([open]), details.ev-info:not([open])'); if (d) d.open = true; }
    })).observe(document.body, { attributes: true, subtree: true, attributeFilter: ['class'] });
    if (document.body) vigilarErrores(); else document.addEventListener('DOMContentLoaded', vigilarErrores);

    function leerInfo(raiz) {
        const b = bloqueInfo(raiz);
        if (!b) return null;
        return Object.fromEntries(INFO.map(([k]) => [k, b.querySelector(`[data-info="${k}"]`)?.value.trim() || null]));
    }

    // ---------- Estado de las ofertas de hotel (CAMBIO 131) ----------
    // EVCodigo.estados(select, valor, { vacio }) llena el <select> (Tom Select) con Confirmada, Bajo petición… y le pone al lado
    // el botón «＋ Nuevo estado»: se escribe el nombre, se crea en la base y queda elegido (y aparece en todos los selects de estado).
    let cacheEstados = null;
    const estadosLista = (refrescar = false) => { if (refrescar || !cacheEstados) cacheEstados = json('/Codigos/EstadosOferta').catch(e => { cacheEstados = null; throw e; }); return cacheEstados; };
    function opcionesEstado(sel, lista, valor, vacio) {
        const v = valor == null ? '' : String(valor);
        sel.innerHTML = `<option value="">${esc(vacio)}</option>` + lista.map(e => `<option value="${e.id}" ${String(e.id) === v ? 'selected' : ''}>${esc(e.nombre)}</option>`).join('');
        sel.value = v; sel.dispatchEvent(new Event('ev:opciones'));
    }
    async function estados(sel, valor, { vacio = '— Sin estado —' } = {}) {
        if (!sel) return;
        sel.dataset.evEstado = '1'; sel.dataset.evVacio = vacio;
        opcionesEstado(sel, await estadosLista(), valor, vacio);
        const f = sel.closest('.ui-f') || sel.parentElement;
        if (f.querySelector('[data-ev-estado-nuevo]')) return;
        const caja = document.createElement('div');
        caja.dataset.evEstadoNuevo = '1';
        caja.style.cssText = 'display:flex;gap:6px;margin-top:6px;align-items:center;flex-wrap:wrap';
        caja.innerHTML = `<button type="button" class="ev-btn ghost sm" data-a="abrir" style="padding:4px 10px;font-size:12.5px"><i class="bi bi-plus-lg"></i> Nuevo estado</button>
            <span data-a="form" hidden style="display:flex;gap:6px;align-items:center;flex:1"><input data-a="nombre" maxlength="60" placeholder="Ej. Lista de espera" style="flex:1;min-width:140px">
            <button type="button" class="ev-btn sm" data-a="crear">Agregar</button><button type="button" class="ev-btn ghost sm" data-a="cancelar">Cancelar</button></span>
            <small data-a="msg" style="font-weight:600"></small>`;
        f.appendChild(caja);
        const q = a => caja.querySelector(`[data-a="${a}"]`);
        const abrir = si => { q('form').hidden = !si; q('form').style.display = si ? 'flex' : 'none'; q('abrir').hidden = si; if (si) q('nombre').focus(); };
        abrir(false);
        q('abrir').onclick = () => abrir(true);
        q('cancelar').onclick = () => { q('nombre').value = ''; q('msg').textContent = ''; abrir(false); };
        const crear = async () => {
            const nombre = q('nombre').value.trim();
            if (nombre.length < 3) { q('msg').textContent = 'Escribe el nombre (al menos 3 letras).'; q('msg').style.color = '#B42318'; return; }
            q('crear').disabled = true;
            try {
                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                const r = await fetch('/Codigos/CrearEstadoOferta', { method: 'POST', headers: { 'Content-Type': 'application/json', RequestVerificationToken: token }, body: JSON.stringify({ Nombre: nombre }) });
                const d = await r.json().catch(() => ({}));
                if (!r.ok) throw new Error(d.mensaje || 'No se pudo agregar el estado.');
                const lista = await estadosLista(true);
                // todos los selects de estado de la página reciben el nuevo; este queda elegido
                document.querySelectorAll('select[data-ev-estado]').forEach(s => opcionesEstado(s, lista, s === sel ? d.estado.id : s.value, s.dataset.evVacio || vacio));
                sel.dispatchEvent(new Event('change', { bubbles: true }));
                q('msg').textContent = d.mensaje; q('msg').style.color = 'var(--ev-deep,#0E8F86)'; q('nombre').value = ''; abrir(false);
            } catch (e) { q('msg').textContent = e.message; q('msg').style.color = '#B42318'; }
            finally { q('crear').disabled = false; }
        };
        q('crear').onclick = crear;
        q('nombre').addEventListener('keydown', e => { if (e.key === 'Enter') { e.preventDefault(); crear(); } });
    }

    return { campo, validar, proveedores, monedas, simbolo, limpiar, nombreProveedor, origen, ORIGEN, info, leerInfo, contarInfo, refrescarInfo, infoContador, abrirConDatos, estados, estadosLista };
})();
