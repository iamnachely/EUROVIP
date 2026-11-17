// Home de la web › Buscador (CAMBIO 134). Pedido de la usuaria: «poder cambiar las secciones del buscador y ponerles orden
// (Hoteles, Vuelos, Circuitos, Excursiones, Traslados, Coches, Cruceros)». Todo visual, se guarda solo:
//   • arrastrar una pestaña la cambia de lugar (la de la izquierda sale primero en la web);
//   • el ojo la muestra u oculta;  • la estrella marca la que se abre al entrar (solo una).
// La web lo lee con Eurovip.spPortalListarBuscadorPestanas (Docs/buscador-pestanas.md).
(() => {
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    let lista = [], caja, guardando = null;

    function estilos() {
        const s = document.createElement('style');
        s.textContent = `
#hwBuscador{margin:14px 0 6px;border:1px dashed var(--ev-line,#CFE3E0);border-radius:18px;padding:12px 14px;background:var(--ev-surface,#fff)}
#hwBuscador .cab{display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin-bottom:8px}
#hwBuscador .cab b{font-size:14px;font-weight:800}#hwBuscador .cab small{color:var(--ev-muted,#66807C);font-weight:600}
#hwBuscador .cab .est{margin-left:auto;font-size:12px;font-weight:700;color:var(--ev-deep,#0E8F86)}
#hwBuscador .tabs{display:flex;gap:6px;flex-wrap:wrap;border-bottom:1px solid var(--ev-line,#E2EFED);padding-bottom:6px}
#hwBuscador .tab{display:flex;align-items:center;gap:7px;border:1px solid var(--ev-line,#E2EFED);border-radius:12px;padding:7px 8px 7px 10px;background:var(--ev-surface,#fff);cursor:grab;user-select:none;font-weight:700;font-size:13.5px}
#hwBuscador .tab.off{opacity:.45;text-decoration:line-through}
#hwBuscador .tab.pred{border-color:#0E8F86;box-shadow:0 0 0 2px rgba(14,143,134,.18)}
#hwBuscador .tab .n{font-size:11px;font-weight:800;color:var(--ev-muted,#66807C)}
#hwBuscador .tab button{border:0;background:none;cursor:pointer;padding:2px 3px;color:var(--ev-muted,#66807C);font-size:14px}
#hwBuscador .tab button:hover{color:var(--ev-ink,#0B1215)} #hwBuscador .tab .estrella.on{color:#F5A623}
#hwBuscador .tab.sortable-ghost{opacity:.3}`;
        document.head.appendChild(s);
    }

    function pintar() {
        caja.innerHTML = `<div class="cab"><b><i class="bi bi-search"></i> Buscador de la web</b><small>Arrastra para ordenar · <i class="bi bi-eye"></i> mostrar u ocultar · <i class="bi bi-star-fill" style="color:#F5A623"></i> la que se abre primero</small><span class="est" id="hwBusEst"></span></div>
            <div class="tabs" id="hwBusTabs">${lista.map((p, i) => `<div class="tab ${p.activo ? '' : 'off'} ${p.predeterminada ? 'pred' : ''}" data-clave="${esc(p.clave)}">
                <span class="n">${i + 1}</span><i class="bi ${esc(p.icono || 'bi-grid')}"></i> ${esc(p.nombre)}
                <button type="button" class="estrella ${p.predeterminada ? 'on' : ''}" data-a="pred" title="${p.predeterminada ? 'Se abre esta al entrar' : 'Abrir esta al entrar'}"><i class="bi ${p.predeterminada ? 'bi-star-fill' : 'bi-star'}"></i></button>
                <button type="button" data-a="ver" title="${p.activo ? 'Ocultar en la web' : 'Mostrar en la web'}"><i class="bi ${p.activo ? 'bi-eye' : 'bi-eye-slash'}"></i></button></div>`).join('')}</div>`;
        const tabs = caja.querySelector('#hwBusTabs');
        if (window.Sortable) Sortable.create(tabs, {
            animation: 150, forceFallback: true, onEnd: () => {
                const orden = [...tabs.children].map(t => t.dataset.clave);
                lista.sort((a, b) => orden.indexOf(a.clave) - orden.indexOf(b.clave));
                guardar();
            }
        });
        tabs.querySelectorAll('button[data-a]').forEach(b => b.addEventListener('click', e => {
            e.stopPropagation();
            const p = lista.find(x => x.clave === b.closest('[data-clave]').dataset.clave);
            if (b.dataset.a === 'ver') {
                if (p.activo && lista.filter(x => x.activo).length === 1) { EV.toast('Tiene que quedar al menos una pestaña visible.', 'err'); return; }
                p.activo = !p.activo;
                if (!p.activo && p.predeterminada) { p.predeterminada = false; const otra = lista.find(x => x.activo); if (otra) otra.predeterminada = true; }
            } else {
                if (!p.activo) { EV.toast('Primero muestra esa pestaña (el ojo).', 'err'); return; }
                lista.forEach(x => x.predeterminada = x === p);
            }
            guardar();
        }));
    }

    async function guardar() {
        pintar();
        const est = caja.querySelector('#hwBusEst'); est.textContent = 'Guardando…';
        clearTimeout(guardando);
        guardando = setTimeout(async () => {
            try {
                const r = await EV.post('/HomeWeb/BuscadorGuardar', lista.map(p => ({ Clave: p.clave, Activo: p.activo, Predeterminada: p.predeterminada })));
                const e2 = caja.querySelector('#hwBusEst'); if (e2) e2.textContent = '✓ ' + r.mensaje;
            } catch (e) { EV.toast(e.message, 'err'); cargar(); }
        }, 350);
    }

    async function cargar() {
        try { lista = await EV.api('/HomeWeb/Buscador'); pintar(); }
        catch (e) { caja.innerHTML = `<small style="color:#B42318;font-weight:600">No pude cargar el buscador: ${esc(e.message)}</small>`; }
    }

    document.addEventListener('DOMContentLoaded', () => {
        caja = document.getElementById('hwBuscador');
        if (!caja) return;
        estilos(); cargar();
    });
})();
