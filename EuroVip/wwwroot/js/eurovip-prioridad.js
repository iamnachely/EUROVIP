// Peso/Prioridad y Precio del proveedor (FASE 29) en todas las cargas: Hoteles, Solo Hotel, Paquetes, Circuitos, Excursiones,
// Traslados y Cruceros.
//   EVPrioridad.estrella(peso)                 → insignia dorada «★ 1» (vacío si no tiene prioridad)
//   EVPrioridad.campos({ peso, proveedor, precio, ayuda }) → HTML de los dos campos del formulario (ids pPeso / pProveedor)
//   EVPrioridad.leer(raiz)                     → { Peso, Precio_Proveedor } de esos campos (null si están vacíos)
//   EVPrioridad.diferencia(nuestro, proveedor) → texto «ahorra RD$ 210 (14 %)» / «RD$ 90 más caro» / ''
//   EVPrioridad.ordenar(lista, peso, precio)   → copia ordenada: primero los de prioridad (1, 2, 3…) y luego por precio
//   EVPrioridad.contexto(input, { que, id, aloj, tipo, destino, contenedor }) → cambia la lista de la que se muestran
//                                                los puestos ocupados (p. ej. al elegir el hotel en una carga)
//   <input data-prio>                          → se convierte sola en el selector de puestos (la casilla queda oculta y
//                                                sigue teniendo el valor; al elegir dispara «input» y «change»)
// Prioridad 1 sale primero en la web, la 2 en segundo lugar…; lo que no tiene prioridad va después, de menor a mayor precio.
window.EVPrioridad = (() => {
    const css = document.createElement('style');
    css.textContent = `
.ev-estrella { display: inline-flex; align-items: center; gap: 4px; padding: 3px 9px 3px 7px; border-radius: 99px; font: 900 12.5px/1 var(--ev-font, system-ui);
    background: linear-gradient(135deg, #FFE08A, #F5A623); color: #5C3A00; box-shadow: 0 2px 8px rgba(245,166,35,.45), inset 0 1px 0 rgba(255,255,255,.6);
    white-space: nowrap; vertical-align: middle; letter-spacing: .02em; }
.ev-estrella i { font-size: 12px; color: #7A4B00; }
.ev-estrella.grande { font-size: 15px; padding: 5px 12px 5px 9px; }
.ev-estrella.grande i { font-size: 15px; }
.ev-con-prioridad { box-shadow: 0 0 0 2px #F5A623, 0 8px 24px -10px rgba(245,166,35,.6) !important; }
.ev-prio-campos { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 12px; padding: 14px; border-radius: 16px;
    background: linear-gradient(135deg, #FFF8E6, #FFFDF7); border: 1px solid #FCE3A6; }
.ev-prio-campos label { display: block; font: 800 12.5px var(--ev-font, system-ui); margin-bottom: 6px; color: #5C3A00; }
.ev-prio-campos label em { font-style: normal; font-weight: 600; color: #8A6A2E; }
.ev-prio-campos input { width: 100%; border: 1.5px solid #F3D48A; border-radius: 12px; padding: 10px 12px; font: 700 14px var(--ev-font, system-ui); background: #fff; }
.ev-prio-campos input:focus { outline: 0; border-color: #F5A623; box-shadow: 0 0 0 3px rgba(245,166,35,.25); }
.ev-prio-campos small { display: block; margin-top: 6px; color: #8A6A2E; font-weight: 600; font-size: 12px; }
.ev-prio-campos > .ev-prio-peso { grid-column: 1 / -1; }
/* selector de puestos */
.ev-pp-row { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.ev-pp { display: inline-flex; align-items: center; gap: 6px; border: 1.5px solid #F3D48A; background: #fff; color: #5C3A00; border-radius: 14px;
    padding: 9px 14px; font: 800 14px/1 var(--ev-font, system-ui); cursor: pointer; transition: transform .15s, box-shadow .2s, background .2s; }
.ev-pp:hover { transform: translateY(-2px); box-shadow: 0 6px 16px -8px rgba(245,166,35,.8); }
.ev-pp .m { font-size: 17px; line-height: 1; }
.ev-pp.on { background: linear-gradient(135deg, #FFE08A, #F5A623); border-color: #E59A12; box-shadow: 0 6px 18px -6px rgba(245,166,35,.85), inset 0 1px 0 rgba(255,255,255,.6); }
.ev-pp.sin { color: #66807C; border-style: dashed; border-color: #D5DEDB; }
.ev-pp.sin.on { background: #F1F5F4; color: #34504B; border-style: solid; border-color: #B9C8C4; box-shadow: none; }
.ev-pp-otro { display: inline-flex; align-items: center; border: 1.5px solid #F3D48A; background: #fff; border-radius: 14px; overflow: hidden; }
.ev-pp-otro button { border: 0; background: transparent; width: 34px; height: 36px; font: 900 17px var(--ev-font, system-ui); color: #7A4B00; cursor: pointer; }
.ev-pp-otro button:hover { background: #FFF3D1; }
.ev-pp-otro b { min-width: 52px; text-align: center; font: 800 13.5px var(--ev-font, system-ui); color: #8A6A2E; }
.ev-pp-otro.on { background: linear-gradient(135deg, #FFE08A, #F5A623); border-color: #E59A12; }
.ev-pp-otro.on b { color: #5C3A00; font-size: 14px; }
.ev-pp .nn { display: inline-flex; flex-direction: column; align-items: flex-start; line-height: 1.1; }
.ev-pp .nn small { font-size: 10.5px; font-weight: 700; color: #8A6A2E; max-width: 110px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; margin-top: 2px; }
.ev-pp.on .nn small { color: #5C3A00; }
.ev-pp.libre { border-style: dashed; background: #F6FEF9; border-color: #A6E9C5; }
.ev-pp.libre .nn small { color: #067647; }
.ev-pp-orden { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; margin-top: 10px; font: 600 12px var(--ev-font, system-ui); color: #8A6A2E; }
.ev-pp-orden span { background: #fff; border: 1px solid #F3D48A; border-radius: 99px; padding: 3px 10px; color: #5C3A00; }
.ev-pp-orden span.yo { background: linear-gradient(135deg, #FFE08A, #F5A623); border-color: #E59A12; font-weight: 800; }
.ev-pp-txt { display: flex; align-items: center; gap: 8px; margin-top: 10px; font: 600 12.5px var(--ev-font, system-ui); color: #8A6A2E; }
.ev-pp-txt i { color: #F5A623; font-size: 14px; }
.ev-pp-txt b { color: #5C3A00; }
.ev-prio-dif { font-weight: 800 !important; }
.ev-prio-dif.bien { color: #166534 !important; } .ev-prio-dif.mal { color: #B42318 !important; }
.ev-proveedor { display: inline-block; font-size: 12px; font-weight: 700; color: #66807C; }
.ev-proveedor s { color: #B42318; }`;
    document.head.appendChild(css);
    const esc = s => (s ?? '').toString().replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const num = v => v === '' || v == null || isNaN(+v) ? null : +v;
    const dinero = n => 'RD$ ' + Number(n).toLocaleString('es-ES', { maximumFractionDigits: 0 });

    const estrella = (peso, grande) => peso ? `<span class="ev-estrella${grande ? ' grande' : ''}" title="Prioridad ${peso}: sale en el lugar ${peso} de la web, antes que lo que no tiene prioridad"><i class="bi bi-star-fill"></i>${peso}</span>` : '';

    function diferencia(nuestro, proveedor) {
        nuestro = num(nuestro); proveedor = num(proveedor);
        if (!nuestro || !proveedor) return { texto: '', clase: '' };
        const d = proveedor - nuestro, pct = Math.round(Math.abs(d) * 100 / proveedor);
        if (Math.abs(d) < 1) return { texto: 'Igual que el proveedor', clase: '' };
        return d > 0 ? { texto: `El cliente ahorra ${dinero(d)} (${pct} %) frente al proveedor`, clase: 'bien' }
                     : { texto: `${dinero(-d)} más caro que el proveedor`, clase: 'mal' };
    }

    // contexto = la lista de la web donde compite: { que: 'oferta'|'hotel'|'tarjeta'|'circuito'|'excursion'|'traslado'|'crucero', id, … }
    function campos({ peso = null, proveedor = null, precio = null, ayuda = '', contexto = null } = {}) {
        const dif = diferencia(precio, proveedor);
        return `<div class="ev-prio-campos">
            <div class="ev-prio-peso"><label><i class="bi bi-star-fill" style="color:#F5A623"></i> Prioridad en la web <em>opcional · elige el puesto</em></label>
                <input id="pPeso" data-prio type="number" min="1" max="999" step="1" value="${peso ?? ''}"${contexto ? ` data-prio-ctx="${esc(JSON.stringify(contexto))}"` : ''}></div>
            <div><label for="pProveedor"><i class="bi bi-shop"></i> Precio del proveedor <em>opcional</em></label>
                <input id="pProveedor" type="number" min="0" step="1" placeholder="Precio en su web" value="${proveedor ?? ''}">
                <small class="ev-prio-dif ${dif.clase}" id="pDif" data-def="${esc('Solo informativo: nuestro precio no cambia.' + (ayuda ? ' ' + ayuda : ''))}">${dif.texto || esc('Solo informativo: nuestro precio no cambia.' + (ayuda ? ' ' + ayuda : ''))}</small></div>
        </div>`;
    }

    /** Conecta el cálculo de la diferencia en vivo; precio() devuelve nuestro precio actual del formulario. */
    function vivo(raiz, precio) {
        const p = raiz.querySelector('#pProveedor'), out = raiz.querySelector('#pDif');
        if (!p || !out) return;
        const pintar = () => { const d = diferencia(precio?.(), p.value); out.textContent = d.texto || out.dataset.def || 'Solo informativo: nuestro precio no cambia.'; out.className = 'ev-prio-dif ' + d.clase; };
        p.addEventListener('input', pintar);
        return pintar;
    }

    function leer(raiz = document) {
        const peso = num(raiz.querySelector('#pPeso')?.value), prov = num(raiz.querySelector('#pProveedor')?.value);
        return { Peso: peso ? Math.round(peso) : null, Precio_Proveedor: prov };
    }

    function ordenar(lista, peso = x => x.peso, precio = x => x.precio) {
        return [...lista].sort((a, b) => {
            const pa = peso(a), pb = peso(b);
            if (pa && pb) return pa - pb;
            if (pa) return -1;
            if (pb) return 1;
            return (precio(a) ?? Infinity) - (precio(b) ?? Infinity);
        });
    }

    /** Línea corta para listas: «Proveedor RD$ 1.500 · ahorra RD$ 210». */
    function proveedor(nuestro, prov) {
        if (!num(prov)) return '';
        const d = diferencia(nuestro, prov);
        return `<span class="ev-proveedor" title="${esc(d.texto)}">Proveedor <s>${dinero(prov)}</s>${d.clase === 'bien' ? ` · <span style="color:#166534">ahorra ${dinero(num(prov) - num(nuestro))}</span>` : ''}</span>`;
    }

    // ---- selector de puestos: sustituye a la casilla de número <input data-prio> ----
    const MEDALLA = { 1: '🥇', 2: '🥈', 3: '🥉' };
    // Sin contexto: puestos 1º–5º y «Otro». Con contexto (data-prio-ctx) pregunta a /Prioridad/Ocupados quién tiene cada
    // puesto en esa lista y solo ofrece 1…(ocupados + 1): cada chip dice de quién es o «libre», y abajo se explica qué pasa
    // al guardar (entra y los demás bajan, o se intercambia). Los puestos nunca se repiten: lo garantiza la base (FASE 37).
    const corto = s => { s = (s || '').trim(); return s.length > 16 ? s.slice(0, 15) + '…' : s; };
    function selector(inp) {
        if (inp.dataset.prioOk) return;
        inp.dataset.prioOk = '1';
        inp.hidden = true;
        const w = document.createElement('div');
        w.className = 'ev-prio-pick';
        inp.after(w);
        const original = Math.round(+inp.value || 0);        // el puesto que tiene guardado
        let ocup = null, ambito = '';                         // null = sin datos de la lista
        const valor = () => Math.max(0, Math.min(999, Math.round(+inp.value || 0)));
        const otros = () => (ocup || []).filter(p => !p.yo);
        const maximo = () => ocup ? otros().length + 1 : 999;
        const dueno = n => ocup?.find(p => p.peso === n && !p.yo) || null;   // quién está HOY en el puesto n (sin contar este registro)
        const explicar = v => {
            if (!v) return original && ocup ? '<i class="bi bi-arrow-up-circle"></i><span>Al guardar <b>pierde la prioridad</b> y los que estaban detrás suben un puesto.</span>'
                : '<i class="bi bi-sort-numeric-down"></i><span>Sin prioridad: se ordena por precio, del más barato al más caro.</span>';
            if (!ocup) return `<i class="bi bi-trophy-fill"></i><span>Sale en el <b>puesto ${v}</b> de la web, antes que todo lo que no tiene prioridad.</span>`;
            if (v === original) return `<i class="bi bi-check-circle-fill"></i><span>Está en el <b>puesto ${v}</b>.</span>`;
            const d = dueno(v);
            if (!d) return `<i class="bi bi-trophy-fill"></i><span>Al guardar queda en el <b>puesto ${v}</b>, el siguiente libre.</span>`;
            return original
                ? `<i class="bi bi-arrow-left-right"></i><span>Al guardar <b>se intercambia</b> con «${esc(d.nombre)}», que pasa al ${original}º.</span>`
                : `<i class="bi bi-arrow-down-circle"></i><span>Al guardar entra en el <b>puesto ${v}</b>; «${esc(d.nombre)}» y los siguientes bajan un puesto.</span>`;
        };
        const pinta = () => {
            const v = valor(), max = maximo(), visibles = Math.min(max, 5);
            const chips = Array.from({ length: visibles }, (_, i) => i + 1).map(n => {
                const d = dueno(n), libre = ocup && !d && n !== original;
                const sub = !ocup ? '' : n === original ? 'este' : d ? corto(d.nombre) : 'libre';
                return `<button type="button" class="ev-pp ${v === n ? 'on' : ''} ${libre ? 'libre' : ''}" data-v="${n}" role="radio" aria-checked="${v === n}"
                    title="${d ? 'Ahora lo tiene ' + esc(d.nombre) : 'Puesto ' + n + ' de la web'}"><span class="m">${MEDALLA[n] || '★'}</span><span class="nn">${n}º${sub ? `<small>${esc(sub)}</small>` : ''}</span></button>`;
            }).join('');
            const otro = max > 5 ? `<span class="ev-pp-otro ${v > 5 ? 'on' : ''}" title="Puesto 6º en adelante (hasta el ${max}º)"><button type="button" data-d="-1" aria-label="Puesto anterior">−</button><b>${v > 5 ? v + 'º' : 'Otro'}</b><button type="button" data-d="1" aria-label="Puesto siguiente">+</button></span>` : '';
            const orden = ocup && ocup.length ? `<div class="ev-pp-orden"><b>Orden actual de ${esc(ambito)}:</b> ${ocup.slice(0, 8).map(p => `<span class="${p.yo ? 'yo' : ''}">${p.peso}º ${esc(corto(p.nombre))}</span>`).join('')}${ocup.length > 8 ? `<span>+${ocup.length - 8} más</span>` : ''}</div>` : '';
            w.innerHTML = `<div class="ev-pp-row" role="radiogroup" aria-label="Prioridad en la web">
                <button type="button" class="ev-pp sin ${v ? '' : 'on'}" data-v="0" role="radio" aria-checked="${!v}"><i class="bi bi-slash-circle"></i> Sin prioridad</button>
                ${chips}${otro}</div>
            <div class="ev-pp-txt">${explicar(v)}</div>${orden}`;
        };
        const poner = n => {
            inp.value = n ? String(n) : '';
            inp.dispatchEvent(new Event('input', { bubbles: true }));
            inp.dispatchEvent(new Event('change', { bubbles: true }));
            pinta();
        };
        w.addEventListener('click', e => {
            const b = e.target.closest('button'); if (!b) return;
            if (b.dataset.v != null) return poner(+b.dataset.v);
            const v = valor(), d = +b.dataset.d, max = maximo();
            poner(Math.max(0, Math.min(max, v <= 5 && d > 0 ? Math.max(6, v + 1) : v + d)));
        });
        inp._prioCargar = async () => {
            let ctx = null;
            try { ctx = JSON.parse(inp.dataset.prioCtx || 'null'); } catch { }
            if (!ctx?.que) { ocup = null; pinta(); return; }
            const q = new URLSearchParams(Object.entries(ctx).filter(([, x]) => x != null && x !== '').map(([k, x]) => [k, String(x)]));
            try {
                const r = await fetch('/Prioridad/Ocupados?' + q, { headers: { Accept: 'application/json' } });
                const j = r.ok ? await r.json() : null;
                ocup = j?.ambito ? j.lista : null; ambito = j?.ambito || '';
            } catch { ocup = null; }
            if (ocup && valor() > maximo()) inp.value = String(maximo());   // un puesto que ya no existe → el siguiente libre
            pinta();
        };
        pinta();
        inp._prioCargar();
    }
    /** Cambia la lista con la que compite (y vuelve a pedir los puestos). */
    function contexto(inp, ctx) {
        if (!inp) return;
        inp.dataset.prioCtx = JSON.stringify(ctx || {});
        inp._prioCargar?.();
    }
    // se activa solo en cualquier <input data-prio> que aparezca en la página (formularios que se pintan después)
    const mejorar = () => document.querySelectorAll('input[data-prio]:not([data-prio-ok])').forEach(selector);
    const vigilar = () => { mejorar(); new MutationObserver(mejorar).observe(document.body, { childList: true, subtree: true }); };
    if (document.body) vigilar(); else document.addEventListener('DOMContentLoaded', vigilar);

    return { estrella, campos, vivo, leer, ordenar, diferencia, proveedor, selector, contexto };
})();
