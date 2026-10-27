// Peso/Prioridad y Precio del proveedor (FASE 29) en todas las cargas: Hoteles, Solo Hotel, Paquetes, Circuitos, Excursiones,
// Traslados y Cruceros.
//   EVPrioridad.estrella(peso)                 → insignia dorada «★ 1» (vacío si no tiene prioridad)
//   EVPrioridad.campos({ peso, proveedor, precio, ayuda }) → HTML de los dos campos del formulario (ids pPeso / pProveedor)
//   EVPrioridad.leer(raiz)                     → { Peso, Precio_Proveedor } de esos campos (null si están vacíos)
//   EVPrioridad.diferencia(nuestro, proveedor) → texto «ahorra RD$ 210 (14 %)» / «RD$ 90 más caro» / ''
//   EVPrioridad.ordenar(lista, peso, precio)   → copia ordenada: primero los de prioridad (1, 2, 3…) y luego por precio
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

    function campos({ peso = null, proveedor = null, precio = null, ayuda = '' } = {}) {
        const dif = diferencia(precio, proveedor);
        return `<div class="ev-prio-campos">
            <div class="ev-prio-peso"><label><i class="bi bi-star-fill" style="color:#F5A623"></i> Prioridad en la web <em>opcional · elige el puesto</em></label>
                <input id="pPeso" data-prio type="number" min="1" max="999" step="1" value="${peso ?? ''}"></div>
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
    function selector(inp) {
        if (inp.dataset.prioOk) return;
        inp.dataset.prioOk = '1';
        inp.hidden = true;
        const w = document.createElement('div');
        w.className = 'ev-prio-pick';
        inp.after(w);
        const valor = () => Math.max(0, Math.min(999, Math.round(+inp.value || 0)));
        const pinta = () => {
            const v = valor();
            w.innerHTML = `<div class="ev-pp-row" role="radiogroup" aria-label="Prioridad en la web">
                <button type="button" class="ev-pp sin ${v ? '' : 'on'}" data-v="0" role="radio" aria-checked="${!v}"><i class="bi bi-slash-circle"></i> Sin prioridad</button>
                ${[1, 2, 3, 4, 5].map(n => `<button type="button" class="ev-pp ${v === n ? 'on' : ''}" data-v="${n}" role="radio" aria-checked="${v === n}" title="Sale en el puesto ${n} de la web"><span class="m">${MEDALLA[n] || '★'}</span>${n}º</button>`).join('')}
                <span class="ev-pp-otro ${v > 5 ? 'on' : ''}" title="Otro puesto (6º en adelante)"><button type="button" data-d="-1" aria-label="Puesto anterior">−</button><b>${v > 5 ? v + 'º' : 'Otro'}</b><button type="button" data-d="1" aria-label="Puesto siguiente">+</button></span>
            </div>
            <div class="ev-pp-txt">${v
                ? `<i class="bi bi-trophy-fill"></i><span>Sale en el <b>puesto ${v}</b> de la web, antes que todo lo que no tiene prioridad.</span>`
                : '<i class="bi bi-sort-numeric-down"></i><span>Sin prioridad: se ordena por precio, del más barato al más caro.</span>'}</div>`;
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
            const v = valor(), d = +b.dataset.d;
            poner(Math.max(0, Math.min(999, v <= 5 && d > 0 ? Math.max(6, v + 1) : v + d)));
        });
        pinta();
    }
    // se activa solo en cualquier <input data-prio> que aparezca en la página (formularios que se pintan después)
    const mejorar = () => document.querySelectorAll('input[data-prio]:not([data-prio-ok])').forEach(selector);
    const vigilar = () => { mejorar(); new MutationObserver(mejorar).observe(document.body, { childList: true, subtree: true }); };
    if (document.body) vigilar(); else document.addEventListener('DOMContentLoaded', vigilar);

    return { estrella, campos, vivo, leer, ordenar, diferencia, proveedor, selector };
})();
