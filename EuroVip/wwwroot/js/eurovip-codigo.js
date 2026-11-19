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
        ['Incluye', 'Incluye', 'Lo que entra en el precio', 'Ej.: Traslado ida y vuelta, desayuno buffet (con viñetas: una cosa por línea)', 'bi-check2-circle'],
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
.ev-info textarea[data-ev-rt]{display:none}
.ev-rt{min-width:0;border:1.5px solid var(--ev-line,#E2EFED);border-radius:11px;background:var(--ev-surface,#fff);transition:border-color .2s,box-shadow .2s}
.ev-rt:focus-within{border-color:var(--ev-teal,#2BD4C4);box-shadow:0 0 0 4px rgba(116,242,230,.3)}
.ev-rt-barra{display:flex;flex-wrap:wrap;align-items:center;gap:2px;padding:4px 6px;border-bottom:1px solid var(--ev-line,#E2EFED);opacity:.7;transition:opacity .2s;position:relative}
.ev-rt:focus-within .ev-rt-barra,.ev-rt:hover .ev-rt-barra{opacity:1}
.ev-rt-barra button{border:0;background:none;min-width:28px;height:28px;padding:0 6px;border-radius:7px;font-size:14px;color:var(--ev-text,#0B1215);cursor:pointer;display:inline-flex;align-items:center;gap:4px}
.ev-rt-barra button:hover{background:var(--ev-surface-2,#F4F8F7)}
.ev-rt-barra button.on{background:var(--ev-ink,#0B1215);color:var(--ev-aqua,#74F2E6)}
.ev-rt-barra select{height:28px;border:1px solid var(--ev-line,#E2EFED);border-radius:7px;padding:0 4px;font:600 12px var(--ev-font,inherit);background:var(--ev-surface,#fff);color:var(--ev-text,inherit);max-width:110px}
.ev-rt-barra .sep{width:1px;height:18px;background:var(--ev-line,#E2EFED);margin:0 3px}
.ev-rt-barra .muestra{display:inline-block;width:12px;height:12px;border-radius:3px;border:1px solid rgba(0,0,0,.15);background:#B42318}
.ev-rt-pop{position:absolute;z-index:30;top:34px;left:6px;display:flex;flex-wrap:wrap;gap:6px;align-items:center;max-width:250px;padding:8px;border-radius:12px;background:var(--ev-surface,#fff);border:1px solid var(--ev-line,#E2EFED);box-shadow:0 12px 30px -12px rgba(11,18,21,.35)}
.ev-rt-pop[hidden]{display:none}
.ev-rt-pop button.c{width:24px;height:24px;min-width:0;padding:0;border-radius:7px;border:1px solid rgba(0,0,0,.12)}
.ev-rt-pop label{display:inline-flex;align-items:center;gap:5px;font-size:12px;font-weight:700;cursor:pointer}
.ev-rt-pop input[type=color]{width:28px;height:24px;padding:0;border:0;background:none;cursor:pointer}
.ev-rt-ed{min-height:40px;max-height:320px;overflow:auto;resize:vertical;padding:8px 11px;font:500 14px var(--ev-font,inherit);line-height:1.45;color:var(--ev-text,inherit);outline:0;word-break:break-word}
.ev-rt-ed:empty::before{content:attr(data-ph);color:var(--ev-muted,#66807C);opacity:.75;pointer-events:none}
.ev-rt-ed p,.ev-rt-ver p{margin:0 0 4px}.ev-rt-ed ul,.ev-rt-ed ol,.ev-rt-ver ul,.ev-rt-ver ol{margin:0 0 4px;padding-left:22px}
.ev-rt-ed ul,.ev-rt-ver ul{list-style:disc}.ev-rt-ed ol,.ev-rt-ver ol{list-style:decimal}
.ev-rt-ver{line-height:1.45;word-break:break-word}
.ev-tabs-info>.ev-info-tabs{margin:0 0 14px}
.ev-tabs-info[data-ev-tab="datos"]>[data-ev-pane-info]{display:none !important}
.ev-tabs-info[data-ev-tab="info"]>:not(.ev-info-tabs):not([data-ev-pane-info]){display:none !important}
@media (max-width:640px){.ev-info .fila{grid-template-columns:1fr;gap:4px}.ev-info .fila>label{padding-top:2px}.ev-rt-barra select{max-width:92px}}`;
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
        // Pedido de la usuaria: cada sección en UNA fila (etiqueta a la izquierda, editor a la derecha) con su barra de formato.
        // El <textarea data-info> sigue existiendo (oculto) y guarda el HTML: así las pantallas lo leen y lo ponen igual que antes.
        const filas = INFO.map(([k, t, ayuda, ph, ico]) => {
            const id = ids[k] || `evi_${k}_${Math.random().toString(36).slice(2, 8)}`;
            return `<div class="fila ${String(val(k)).trim() ? 'llena' : ''}"><label for="${esc(id)}"><i class="bi ${ico}"></i><span>${esc(t)}<small>${esc(ayuda)}</small></span></label>`
                + `<div class="ev-rt">${barraRt()}<div class="ev-rt-ed" contenteditable="true" role="textbox" aria-multiline="true" aria-label="${esc(t)}" data-ph="${esc(ph)}" spellcheck="true"></div>`
                + `<textarea id="${esc(id)}" data-info="${k}" data-ev-rt ${attrs[k] || ''} rows="1" hidden>${esc(val(k))}</textarea></div></div>`;
        }).join('');
        const cuerpo = `<p class="ayu">Nada aquí es obligatorio: escribe solo lo que quieras que el cliente lea. Usa la barra de cada texto para el tamaño, la negrita, los colores, las viñetas o la numeración: en la web se ve igual.</p><div class="filas">${filas}</div>`;
        return plano
            ? `<div class="ev-info plano" data-ev-info data-siempre><div class="cab">${cab}</div>${cuerpo}</div>`
            : `<details class="ev-info" data-ev-info data-siempre ${llenos ? 'open' : ''}><summary>${cab}<i class="bi bi-chevron-down chev"></i></summary>${cuerpo}</details>`;
    }
    // ---------- Editor con formato (barra: tamaño, negrita, cursiva, subrayado, colores, viñetas, numeración, limpiar) ----------
    // El <textarea data-info> oculto guarda el HTML (es lo que leen las pantallas y se manda al servidor, que lo sanea otra vez).
    // El <div contenteditable> es lo que ve la usuaria: siempre con sus efectos, nunca las etiquetas.
    const COLORES = ['#0B1215', '#B42318', '#E8590C', '#B54708', '#067647', '#0E9384', '#1570EF', '#5925DC', '#C11574', '#66807C'];
    const RT_TAGS = new Set(['P', 'BR', 'STRONG', 'B', 'EM', 'I', 'U', 'SPAN', 'UL', 'OL', 'LI', 'DIV']);
    const RT_CSS = new Set(['color', 'background-color', 'font-size', 'font-weight']);
    const RT_TAM = { 1: '10px', 2: '12px', 3: '14px', 4: '16px', 5: '18px', 6: '24px', 7: '24px' };
    const nativoValue = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value');
    const esHtmlRt = v => /<\s*\/?\s*[a-zA-Z][a-zA-Z0-9]*(\s[^<>]*)?\/?\s*>/.test(v || '');
    const rtBtn = (c, html, tit) => `<button type="button" data-rt="${c}" title="${tit}" aria-label="${tit}">${html}</button>`;
    const barraRt = () => `<div class="ev-rt-barra" role="toolbar" aria-label="Formato del texto">`
        + `<select data-rt="tam" title="Tamaño de letra" aria-label="Tamaño de letra"><option value="">Tamaño</option><option value="12px">Pequeña</option><option value="14px">Normal</option><option value="18px">Grande</option><option value="24px">Muy grande</option></select>`
        + `<span class="sep"></span>` + rtBtn('bold', '<b>N</b>', 'Negrita') + rtBtn('italic', '<i>K</i>', 'Cursiva') + rtBtn('underline', '<u>S</u>', 'Subrayado')
        + `<span class="sep"></span>` + rtBtn('color', '<b style="color:#B42318">A</b>', 'Color de letra') + rtBtn('resaltar', '<span class="muestra" style="background:#FEF0C7"></span>', 'Resaltar')
        + `<span class="sep"></span>` + rtBtn('ul', '<i class="bi bi-list-ul"></i>', 'Viñetas') + rtBtn('ol', '<i class="bi bi-list-ol"></i>', 'Numeración')
        + `<span class="sep"></span>` + rtBtn('limpiar', '<i class="bi bi-eraser"></i>', 'Quitar formato')
        + `<div class="ev-rt-pop" hidden></div></div>`;
    // HTML → HTML solo con la lista blanca (el mismo criterio del servidor): para lo que viene de la base antes de mostrarlo
    function rtLimpio(v) {
        if (!esHtmlRt(v)) return String(v || '').split(/\r?\n/).map(l => l.trim()).filter(Boolean).map(l => `<p>${esc(l)}</p>`).join('');
        const t = document.createElement('template'); t.innerHTML = v;
        const pasa = n => {
            [...n.childNodes].forEach(h => {
                if (h.nodeType === 3) return;
                if (h.nodeType !== 1) { h.remove(); return; }
                if (/^(SCRIPT|STYLE|IFRAME|OBJECT|EMBED|NOSCRIPT|TEMPLATE|SVG|MATH|TEXTAREA|SELECT|BUTTON|TITLE|HEAD|IMG|VIDEO|AUDIO|FORM|INPUT|LINK|META)$/.test(h.tagName)) { h.remove(); return; }
                pasa(h);
                if (!RT_TAGS.has(h.tagName)) { h.replaceWith(...h.childNodes); return; }
                const st = h.getAttribute('style');
                [...h.attributes].forEach(a => h.removeAttribute(a.name));
                if (st) {
                    const ok = st.split(';').map(s => s.trim()).filter(s => RT_CSS.has((s.split(':')[0] || '').trim().toLowerCase()) && !/url\s*\(|expression|javascript/i.test(s)).join(';');
                    if (ok) h.setAttribute('style', ok);
                }
            });
        };
        pasa(t.content);
        return t.innerHTML;
    }
    function rtSync(ed) {
        // <font size=7> (lo que crea el navegador al cambiar el tamaño) → <span style="font-size:…">
        ed.querySelectorAll('font').forEach(f => {
            const s = document.createElement('span');
            const px = f.dataset.px || RT_TAM[f.getAttribute('size')];
            if (px) s.style.fontSize = px;
            s.append(...f.childNodes); f.replaceWith(s);
        });
        const ta = ed.parentNode.querySelector('textarea[data-info]');
        const vacio = !ed.textContent.replace(/​/g, '').trim() && !ed.querySelector('li');
        ta._rtLock = true; ta.value = vacio ? '' : ed.innerHTML.replace(/​/g, ''); ta._rtLock = false;
        ta.dispatchEvent(new Event('input', { bubbles: true }));
    }
    function rtEstado(ed) {
        const barra = ed.parentNode.querySelector('.ev-rt-barra');
        [['bold', 'bold'], ['italic', 'italic'], ['underline', 'underline'], ['ul', 'insertUnorderedList'], ['ol', 'insertOrderedList']].forEach(([k, c]) => {
            let on = false; try { on = document.queryCommandState(c); } catch { }
            barra.querySelector(`[data-rt="${k}"]`)?.classList.toggle('on', on && ed.contains(document.getSelection()?.anchorNode));
        });
    }
    function rtIniciar(rt) {
        if (rt.dataset.ok) return;
        rt.dataset.ok = '1';
        const ed = rt.querySelector('.ev-rt-ed'), ta = rt.querySelector('textarea[data-info]'), barra = rt.querySelector('.ev-rt-barra'), pop = rt.querySelector('.ev-rt-pop');
        const pinta = v => { ed.innerHTML = rtLimpio(v); };
        pinta(nativoValue.get.call(ta));
        // quien ponga .value por código (al editar un producto) también refresca lo que se ve
        Object.defineProperty(ta, 'value', { configurable: true, get() { return nativoValue.get.call(ta); }, set(v) { nativoValue.set.call(ta, v); if (!ta._rtLock) pinta(v); } });
        let rango = null;
        const guardar = () => { const s = document.getSelection(); if (s.rangeCount && ed.contains(s.anchorNode)) { rango = s.getRangeAt(0).cloneRange(); rtEstado(ed); } };
        const volver = () => { ed.focus(); if (rango) { const s = document.getSelection(); s.removeAllRanges(); s.addRange(rango); } };
        document.addEventListener('selectionchange', () => { if (ed.contains(document.getSelection()?.anchorNode)) guardar(); });
        try { document.execCommand('defaultParagraphSeparator', false, 'p'); } catch { }
        ed.addEventListener('input', () => rtSync(ed));
        ed.addEventListener('blur', () => rtSync(ed));
        ed.addEventListener('paste', e => { e.preventDefault(); const t = (e.clipboardData || window.clipboardData).getData('text/plain'); document.execCommand('insertText', false, t); });
        const ejecutar = (c, v, css) => { volver(); document.execCommand('styleWithCSS', false, !!css); document.execCommand(c, false, v); guardar(); rtSync(ed); };
        const abrirPop = modo => {
            if (!pop.hidden && pop.dataset.modo === modo) { pop.hidden = true; return; }
            pop.dataset.modo = modo; pop.hidden = false;
            pop.innerHTML = COLORES.map(c => `<button type="button" class="c" data-c="${c}" style="background:${c}" title="${c}" aria-label="Color ${c}"></button>`).join('')
                + (modo === 'resaltar' ? ['#FEF0C7', '#D1FADF', '#D1E9FF', '#FCE7F6'].map(c => `<button type="button" class="c" data-c="${c}" style="background:${c}" aria-label="Resaltado ${c}"></button>`).join('') : '')
                + `<label>Otro <input type="color" data-c-otro value="#1570EF"></label><button type="button" data-c="" title="Quitar">Quitar</button>`;
        };
        const aplicarColor = c => {
            const modo = pop.dataset.modo; pop.hidden = true;
            if (modo === 'color') ejecutar('foreColor', c || '#0B1215', true); else ejecutar('hiliteColor', c || 'transparent', true);
        };
        barra.addEventListener('mousedown', e => { if (!e.target.closest('select, input')) e.preventDefault(); });
        barra.addEventListener('click', e => {
            const b = e.target.closest('button'); if (!b) return;
            const c = b.dataset.c;
            if (c !== undefined) { aplicarColor(c); return; }
            switch (b.dataset.rt) {
                case 'bold': ejecutar('bold'); break;
                case 'italic': ejecutar('italic'); break;
                case 'underline': ejecutar('underline'); break;
                case 'ul': ejecutar('insertUnorderedList'); break;
                case 'ol': ejecutar('insertOrderedList'); break;
                case 'limpiar': ejecutar('removeFormat'); break;
                case 'color': case 'resaltar': guardar(); abrirPop(b.dataset.rt); break;
            }
        });
        barra.addEventListener('change', e => {
            if (e.target.matches('[data-c-otro]')) { aplicarColor(e.target.value); return; }
            if (e.target.dataset.rt !== 'tam' || !e.target.value) return;
            volver(); document.execCommand('styleWithCSS', false, false); document.execCommand('fontSize', false, '7');
            ed.querySelectorAll('font[size="7"]').forEach(f => { f.dataset.px = e.target.value; });
            e.target.value = ''; rtSync(ed);
        });
        document.addEventListener('mousedown', e => { if (!pop.hidden && !e.target.closest('.ev-rt-barra') ) pop.hidden = true; });
        ed.addEventListener('keydown', e => { if (e.key === 'Escape' && !pop.hidden) { pop.hidden = true; e.stopPropagation(); } });
    }
    function rtBuscar(raiz) { (raiz.nodeType === 1 ? [...(raiz.matches('.ev-rt') ? [raiz] : []), ...raiz.querySelectorAll('.ev-rt')] : []).forEach(rtIniciar); }
    const rtVigilar = () => { rtBuscar(document.body); new MutationObserver(ms => ms.forEach(m => m.addedNodes.forEach(n => n.nodeType === 1 && rtBuscar(n)))).observe(document.body, { childList: true, subtree: true }); };
    if (document.body) rtVigilar(); else document.addEventListener('DOMContentLoaded', rtVigilar);

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
        if (!t.hasAttribute('data-ev-rt')) crecer(t);
        refrescarInfo(t.closest('[data-ev-info]'));
    });
    document.addEventListener('focusin', e => { if (e.target?.matches?.('[data-ev-info] textarea[data-info]:not([data-ev-rt])')) crecer(e.target); });
    // Contador para un tab o un título: «Información para el cliente (2)». Sigue al bloque aunque se vuelva a pintar.
    function infoContador(el, raiz) {
        if (!el || !raiz) return;
        const pinta = () => { const n = contarInfo(raiz); el.textContent = n ? `${n}/${INFO.length}` : 'opcional'; el.classList.toggle('si', n > 0); };
        raiz.addEventListener('ev:info', pinta);
        new MutationObserver(pinta).observe(raiz, { childList: true, subtree: true });
        pinta();
        return pinta;
    }
    // EVCodigo.tabsInfo(cuerpo, panelInfo, { datos }) → pone dos tabs arriba de un editor (cajón): «Datos» (todo lo demás) y
    // «Información para el cliente» (solo panelInfo). Pedido de la usuaria: esa información SIEMPRE en un tab aparte, al crear y al editar.
    function tabsInfo(cuerpo, panel, { datos = 'Datos del producto', contador } = {}) {
        cssDeInfo();
        if (!cuerpo || !panel || cuerpo.querySelector(':scope > .ev-info-tabs')) return;
        cuerpo.classList.add('ev-tabs-info'); cuerpo.dataset.evTab = 'datos'; panel.setAttribute('data-ev-pane-info', '');
        const barra = document.createElement('div');
        barra.className = 'ui-tabs ev-info-tabs'; barra.setAttribute('role', 'tablist');
        barra.innerHTML = `<button type="button" class="on" data-t="datos" role="tab"><i class="bi bi-ui-checks"></i> ${esc(datos)}</button>`
            + `<button type="button" data-t="info" role="tab"><i class="bi bi-info-circle"></i> Información para el cliente <span class="n" data-ev-tab-n>opcional</span></button>`;
        barra.addEventListener('click', e => {
            const b = e.target.closest('button[data-t]'); if (!b) return;
            cuerpo.dataset.evTab = b.dataset.t;
            barra.querySelectorAll('button').forEach(x => x.classList.toggle('on', x === b));
            cuerpo.scrollTop = 0;
        });
        cuerpo.prepend(barra);
        infoContador(barra.querySelector('[data-ev-tab-n]'), panel);
        // al volver a abrir el editor siempre empieza en «Datos»
        const volver = () => { cuerpo.dataset.evTab = 'datos'; barra.querySelectorAll('button').forEach(x => x.classList.toggle('on', x.dataset.t === 'datos')); };
        return volver;
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

    return { tabsInfo, campo, validar, proveedores, monedas, simbolo, limpiar, nombreProveedor, origen, ORIGEN, info, leerInfo, contarInfo, refrescarInfo, infoContador, abrirConDatos, estados, estadosLista };
})();
