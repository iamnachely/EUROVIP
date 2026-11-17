// Home de la web › Buscador (CAMBIO 134). Pedido de la usuaria: «poder cambiar las secciones del buscador y ponerles orden
// (Hoteles, Vuelos, Circuitos, Excursiones, Traslados, Coches, Cruceros)» y «mejorarlo visualmente». Se ve como el buscador
// real de la web y se guarda solo:
//   • arrastrar una pestaña la cambia de lugar (la de la izquierda sale primero en la web);
//   • el ojo la oculta (pasa a «Ocultas en la web», de donde se vuelve a mostrar con un clic);
//   • la estrella marca la que se abre al entrar (subrayada, como en la web). Solo una.
// La web lo lee con Eurovip.spPortalListarBuscadorPestanas (Docs/buscador-pestanas.md).
(() => {
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    // Lo que muestra el formulario de muestra de cada pestaña (como en la web)
    const MUESTRA = {
        hoteles: [['Destino', '¿A dónde viajas?'], ['Entrada', 'Elige la fecha'], ['Salida', 'Elige la fecha'], ['Viajeros', '1 hab., 2 adultos']],
        vuelos: [['Origen', 'Santo Domingo (SDQ)'], ['Destino', '¿A dónde vuelas?'], ['Fechas', 'Ida y vuelta'], ['Pasajeros', '2 adultos']],
        circuitos: [['Elige tu circuito', 'Descubre tu próximo viaje'], ['Salida', 'Elige un circuito'], ['Regreso', 'Según tu salida'], ['Viajeros', '1 hab., 2 adultos']],
        excursiones: [['Destino', 'Punta Cana, La Romana…'], ['Fecha', 'Elige el día'], ['Personas', '2 adultos']],
        traslados: [['Desde', 'Aeropuerto'], ['Hasta', 'Tu hotel'], ['Llegada', 'Fecha y hora'], ['Pasajeros', '2 adultos']],
        coches: [['Recogida', 'Aeropuerto o ciudad'], ['Desde', 'Fecha y hora'], ['Hasta', 'Fecha y hora']],
        cruceros: [['Destino', 'Caribe, Mediterráneo…'], ['Mes de salida', 'Cualquier mes'], ['Naviera', 'Todas']]
    };
    let lista = [], caja, temporizador = null, estado = 'listo';

    function estilos() {
        if (document.getElementById('hwb-css')) return;
        const s = document.createElement('style'); s.id = 'hwb-css';
        s.textContent = `
#hwBuscador{margin:16px 0 8px;border-radius:22px;padding:16px 18px 18px;background:linear-gradient(180deg,#F5FBFA 0%,#fff 70%);border:1px solid var(--ev-line,#E2EFED);box-shadow:0 12px 30px -24px rgba(11,18,21,.45);position:relative}
.hwb-cab{display:flex;gap:12px;align-items:flex-start;flex-wrap:wrap;margin-bottom:14px}
.hwb-ico{width:40px;height:40px;border-radius:13px;display:grid;place-items:center;background:var(--ev-ink,#0B1215);color:#74F2E6;font-size:18px;flex:none}
.hwb-tit{min-width:0;flex:1}
.hwb-tit b{display:block;font-size:15.5px;font-weight:800;letter-spacing:-.2px}
.hwb-tit small{display:block;color:var(--ev-muted,#66807C);font-weight:600;font-size:12.5px;line-height:1.4;margin-top:2px}
.hwb-est{display:inline-flex;align-items:center;gap:6px;border-radius:999px;padding:5px 11px;font-size:12px;font-weight:800;white-space:nowrap;background:#E6FBF8;color:#0E8F86;transition:background .2s,color .2s}
.hwb-est.guardando{background:#FEF7C3;color:#854D0E}.hwb-est.error{background:#FEE4E2;color:#B42318}
.hwb-est .pt{width:7px;height:7px;border-radius:50%;background:currentColor}
.hwb-est.guardando .pt{animation:hwbpul 1s infinite}@keyframes hwbpul{50%{opacity:.25}}
.hwb-web{background:#fff;border:1px solid var(--ev-line,#E2EFED);border-radius:18px;padding:6px 10px 12px}
.hwb-tabs{display:flex;gap:2px 4px;flex-wrap:wrap;border-bottom:1px solid #E6EDF0;padding-top:12px;align-items:stretch}
.hwb-tab{position:relative;flex:none;display:flex;align-items:center;gap:7px;padding:11px 12px 12px;font-weight:700;font-size:13.5px;color:#475467;cursor:grab;user-select:none;border-radius:12px 12px 0 0;transition:background .15s,color .15s}
.hwb-tab:hover{background:#F2F7F7;color:#0B1215}
.hwb-tab:active{cursor:grabbing}
.hwb-tab i.ic{font-size:16px}
.hwb-tab .gr{color:#B8C7C5;font-size:13px;margin:0 -4px 0 -6px;width:10px;opacity:0;transition:opacity .15s}
.hwb-tab:hover .gr{opacity:1}
.hwb-tab.pred{color:#0B3B4F}
.hwb-tab.pred::after{content:'';position:absolute;left:10px;right:10px;bottom:-1px;height:3px;border-radius:3px;background:#0B3B4F}

.hwb-tab .acc{position:absolute;right:2px;top:-12px;display:none;gap:1px;background:#fff;border-radius:999px;padding:2px;box-shadow:0 4px 12px rgba(11,18,21,.18);z-index:2}
.hwb-tab:hover .acc,.hwb-tab:focus-within .acc{display:flex}
.hwb-tab .pr{color:#F5A623;font-size:11px;margin-left:-3px}
.hwb-tab .acc button{border:0;background:none;width:24px;height:24px;border-radius:8px;display:grid;place-items:center;cursor:pointer;color:#9AB0AD;font-size:13px;transition:background .15s,color .15s}
.hwb-tab .acc button:hover{background:#F2F7F7;color:#0B1215}
.hwb-tab .acc .on{color:#F5A623}
.hwb-tab.sortable-ghost{opacity:.35;background:#E6FBF8}
.hwb-tab.sortable-chosen{background:#E6FBF8}
.hwb-badge{position:absolute;top:-11px;transition:opacity .15s;left:50%;transform:translateX(-50%);background:#0B3B4F;color:#fff;font-size:9.5px;font-weight:800;border-radius:999px;padding:2px 7px;white-space:nowrap;letter-spacing:.02em}
.hwb-form{display:flex;align-items:stretch;gap:0;margin-top:12px;border:1px solid #E6EDF0;border-radius:999px;padding:6px 6px 6px 18px;background:#fff;box-shadow:0 6px 18px -14px rgba(11,18,21,.35);opacity:.9}
.hwb-form .c{flex:1;min-width:0;padding:4px 12px;border-left:1px solid #EEF2F4}
.hwb-form .c:first-child{border-left:0;padding-left:0}
.hwb-form .c b{display:block;font-size:11.5px;font-weight:800;color:#0B1215;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.hwb-form .c span{display:block;font-size:12px;color:#98A2B3;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.hwb-form .ir{flex:none;border-radius:999px;background:#E8A27C;color:#fff;font-weight:800;font-size:12.5px;padding:0 16px;display:flex;align-items:center;gap:6px}
.hwb-pie{display:flex;gap:12px;align-items:center;flex-wrap:wrap;margin-top:12px}
.hwb-ocultas{display:flex;gap:6px;flex-wrap:wrap;align-items:center;flex:1;min-width:0}
.hwb-ocultas>small{font-size:11.5px;font-weight:800;text-transform:uppercase;letter-spacing:.05em;color:#9AB0AD;margin-right:2px}
.hwb-oc{display:inline-flex;align-items:center;gap:6px;border:1px dashed #C9D7D5;border-radius:999px;padding:5px 11px;background:#fff;color:#98A2B3;font-weight:700;font-size:12.5px;cursor:pointer;transition:border-color .15s,color .15s}
.hwb-oc:hover{border-color:#0E8F86;color:#0E8F86}
.hwb-leyenda{display:flex;gap:12px;flex-wrap:wrap;font-size:11.5px;font-weight:700;color:#98A2B3}
.hwb-leyenda i{margin-right:3px}
@media (max-width:640px){.hwb-form{border-radius:18px;flex-wrap:wrap;padding:10px}.hwb-form .c{flex:1 1 45%;border-left:0;padding:4px 6px}.hwb-form .ir{flex:1 1 100%;justify-content:center;padding:9px}}`;
        document.head.appendChild(s);
    }

    const pred = () => lista.find(p => p.predeterminada && p.activo) || lista.find(p => p.activo);

    function pintar() {
        const visibles = lista.filter(p => p.activo), ocultas = lista.filter(p => !p.activo), p0 = pred();
        const campos = MUESTRA[p0?.clave] || MUESTRA.hoteles;
        caja.innerHTML = `
            <div class="hwb-cab"><span class="hwb-ico"><i class="bi bi-search"></i></span>
                <div class="hwb-tit"><b>Buscador de la web</b><small>Así lo ven tus clientes. Arrastra las pestañas para cambiar el orden (la de la izquierda sale primero). Pasa el ratón por una pestaña para ocultarla o hacer que se abra primero.</small></div>
                <span class="hwb-est ${estado}" id="hwbEst"><span class="pt"></span>${estado === 'guardando' ? 'Guardando…' : estado === 'error' ? 'No se guardó' : 'Guardado'}</span></div>
            <div class="hwb-web">
                <div class="hwb-tabs" id="hwbTabs">${visibles.map((p, i) => `<div class="hwb-tab ${p === p0 ? 'pred' : ''}" data-clave="${esc(p.clave)}" title="Arrástrala para moverla">
                    <i class="bi bi-grip-vertical gr"></i><i class="bi ${esc(p.icono || 'bi-grid')} ic"></i>${esc(p.nombre)}${p === p0 ? '<i class="bi bi-star-fill pr" title="Se abre primero"></i>' : ''}
                    <span class="acc"><button type="button" data-a="pred" class="${p === p0 ? 'on' : ''}" title="${p === p0 ? 'Es la que se abre al entrar' : 'Abrir esta al entrar'}"><i class="bi ${p === p0 ? 'bi-star-fill' : 'bi-star'}"></i></button>
                        <button type="button" data-a="ver" class="ojo" title="Ocultar en la web"><i class="bi bi-eye-slash"></i></button></span></div>`).join('')}</div>
                <div class="hwb-form" aria-hidden="true">${campos.map(([t, v]) => `<div class="c"><b>${esc(t)}</b><span>${esc(v)}</span></div>`).join('')}<span class="ir"><i class="bi bi-search"></i> Buscar</span></div>
            </div>
            <div class="hwb-pie">
                <div class="hwb-ocultas">${ocultas.length ? `<small>Ocultas en la web</small>${ocultas.map(p => `<button type="button" class="hwb-oc" data-mostrar="${esc(p.clave)}" title="Volver a mostrarla"><i class="bi ${esc(p.icono || 'bi-grid')}"></i>${esc(p.nombre)}<i class="bi bi-plus-lg"></i></button>`).join('')}` : ''}</div>
                <div class="hwb-leyenda"><span><i class="bi bi-arrows-move"></i>Arrastrar: orden</span><span><i class="bi bi-star-fill" style="color:#F5A623"></i>Se abre primero</span><span><i class="bi bi-eye-slash"></i>Ocultar</span></div>
            </div>`;

        const tabs = caja.querySelector('#hwbTabs');
        if (window.Sortable) Sortable.create(tabs, {
            animation: 160, forceFallback: true, filter: 'button', preventOnFilter: false, onEnd: () => {
                const orden = [...tabs.children].map(t => t.dataset.clave);
                // las ocultas conservan su lugar relativo detrás de las visibles
                lista.sort((a, b) => (orden.includes(a.clave) ? orden.indexOf(a.clave) : 100 + lista.indexOf(a)) - (orden.includes(b.clave) ? orden.indexOf(b.clave) : 100 + lista.indexOf(b)));
                guardar();
            }
        });
        tabs.querySelectorAll('button[data-a]').forEach(b => b.addEventListener('click', e => {
            e.stopPropagation();
            const p = lista.find(x => x.clave === b.closest('[data-clave]').dataset.clave);
            if (b.dataset.a === 'ver') {
                if (lista.filter(x => x.activo).length === 1) { EV.toast('Tiene que quedar al menos una pestaña visible.', 'err'); return; }
                p.activo = false;
                if (p.predeterminada) { p.predeterminada = false; const otra = lista.find(x => x.activo); if (otra) otra.predeterminada = true; }
            } else lista.forEach(x => x.predeterminada = x === p);
            guardar();
        }));
        caja.querySelectorAll('[data-mostrar]').forEach(b => b.addEventListener('click', () => {
            const p = lista.find(x => x.clave === b.dataset.mostrar); p.activo = true; guardar();
        }));
    }

    function guardar() {
        estado = 'guardando'; pintar();
        clearTimeout(temporizador);
        temporizador = setTimeout(async () => {
            try {
                await EV.post('/HomeWeb/BuscadorGuardar', lista.map(p => ({ Clave: p.clave, Activo: p.activo, Predeterminada: p.predeterminada })));
                estado = 'listo';
            } catch (e) { estado = 'error'; EV.toast(e.message, 'err'); await cargar(); return; }
            const est = caja.querySelector('#hwbEst');
            if (est) { est.className = 'hwb-est'; est.innerHTML = '<span class="pt"></span>Guardado'; }
        }, 350);
    }

    async function cargar() {
        try { lista = await EV.api('/HomeWeb/Buscador'); pintar(); }
        catch (e) { caja.innerHTML = `<small style="color:#B42318;font-weight:600"><i class="bi bi-exclamation-triangle"></i> No pude cargar el buscador: ${esc(e.message)}</small>`; }
    }

    document.addEventListener('DOMContentLoaded', () => {
        caja = document.getElementById('hwBuscador');
        if (!caja) return;
        estilos(); cargar();
    });
})();
