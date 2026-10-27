// Peso/Prioridad y Precio del proveedor (FASE 29) en todas las cargas: Hoteles, Solo Hotel, Paquetes, Circuitos, Excursiones,
// Traslados y Cruceros.
//   EVPrioridad.estrella(peso)                 → insignia dorada «★ 1» (vacío si no tiene prioridad)
//   EVPrioridad.campos({ peso, proveedor, precio, ayuda }) → HTML de los dos campos del formulario (ids pPeso / pProveedor)
//   EVPrioridad.leer(raiz)                     → { Peso, Precio_Proveedor } de esos campos (null si están vacíos)
//   EVPrioridad.diferencia(nuestro, proveedor) → texto «ahorra RD$ 210 (14 %)» / «RD$ 90 más caro» / ''
//   EVPrioridad.ordenar(lista, peso, precio)   → copia ordenada: primero los de prioridad (1, 2, 3…) y luego por precio
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
            <div><label for="pPeso"><i class="bi bi-star-fill" style="color:#F5A623"></i> Prioridad <em>opcional</em></label>
                <input id="pPeso" type="number" min="1" max="999" step="1" placeholder="Ej. 1" value="${peso ?? ''}">
                <small>1 sale primero en la web, 2 segundo… Sin prioridad se ordena por precio.${ayuda ? ' ' + esc(ayuda) : ''}</small></div>
            <div><label for="pProveedor"><i class="bi bi-shop"></i> Precio del proveedor <em>opcional</em></label>
                <input id="pProveedor" type="number" min="0" step="1" placeholder="Precio en su web" value="${proveedor ?? ''}">
                <small class="ev-prio-dif ${dif.clase}" id="pDif">${dif.texto || 'Solo informativo: nuestro precio no cambia.'}</small></div>
        </div>`;
    }

    /** Conecta el cálculo de la diferencia en vivo; precio() devuelve nuestro precio actual del formulario. */
    function vivo(raiz, precio) {
        const p = raiz.querySelector('#pProveedor'), out = raiz.querySelector('#pDif');
        if (!p || !out) return;
        const pintar = () => { const d = diferencia(precio?.(), p.value); out.textContent = d.texto || 'Solo informativo: nuestro precio no cambia.'; out.className = 'ev-prio-dif ' + d.clase; };
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

    return { estrella, campos, vivo, leer, ordenar, diferencia, proveedor };
})();
