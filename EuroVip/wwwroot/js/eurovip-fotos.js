// =====================================================================================================
// EVFotos — cambiar las fotos de cualquier cosa desde cualquier pantalla (se guardan al momento).
//   EVFotos.montar(elemento, { tipo, clave, alCambiar })   -> dentro de otro panel (p. ej. la tarjeta de la Home)
//   EVFotos.abrir({ tipo, clave, titulo, alCambiar })       -> en su propio panel lateral
// tipo = el de Eurovip.multimedia: INICIO (tarjeta de la Home), HOTEL, CIRCUITO, EXCURSION, TRASLADO, DESTINO, CRUCERO…
// Los circuitos muestran además las fotos de cada día del itinerario (circuito_itinerario.caratula).
// Cada foto es SOLO de su dueño: cambiar la de una tarjeta no cambia la del destino ni la de otras tarjetas.
// Usa EV (eurovip-ui.js), EVProgreso (eurovip-progreso.js) y EVMultimedia (eurovip-multimedia.js).
// =====================================================================================================
window.EVFotos = (() => {
    const esc = s => (s ?? '').toString().replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const TIPOS = /^image\/(jpeg|png|webp)$/;

    // Qué partes tiene cada tipo y dónde se edita completo
    const CFG = {
        INICIO:    { galeria: false, nombre: 'la tarjeta', ayuda: 'Es la foto de ESTA tarjeta de la Home: no cambia la del destino ni la de otras tarjetas.' },
        HOTEL:     { nombre: 'el hotel', modulo: ['Hoteles', '/Hoteles/Index'] },
        CIRCUITO:  { intermedia: true, nombre: 'el circuito', dias: true, modulo: ['Circuitos', '/Circuitos/Index'] },
        EXCURSION: { nombre: 'la excursión', modulo: ['Excursiones', '/Excursiones/Index'] },
        TRASLADO:  { nombre: 'el traslado', modulo: ['Traslados', '/Traslados/Index'] },
        CRUCERO:   { intermedia: true, nombre: 'el crucero', modulo: ['Cruceros', '/Cruceros/Index'] },
        DESTINO:   { galeria: false, nombre: 'el destino' },
        ALOJAMIENTO: { nombre: 'el alojamiento' },
        PAIS:      { galeria: false, nombre: 'el país' }
    };

    function estilos() {
        if (document.getElementById('evfotos-css')) return;
        const st = document.createElement('style');
        st.id = 'evfotos-css';
        st.textContent = `
        .evf { display: grid; gap: 14px; }
        .evf-ayuda { font-size: 12.5px; font-weight: 600; color: #0E6F68; background: #ECFEFB; border: 1px solid #B3FFF3; border-radius: 12px; padding: 9px 12px; display: flex; gap: 8px; }
        .evf-dias { border: 1px solid var(--ev-line); border-radius: 18px; padding: 14px; background: var(--ev-surface); }
        .evf-dias h4 { margin: 0 0 10px; font-size: 14.5px; display: flex; gap: 8px; align-items: center; }
        .evf-dias h4 small { color: var(--ev-muted); font-weight: 600; font-size: 12px; }
        .evf-lista { display: grid; grid-template-columns: repeat(auto-fill, minmax(170px, 1fr)); gap: 10px; }
        .evf-dia { position: relative; border-radius: 12px; overflow: hidden; border: 1px solid var(--ev-line); background: var(--ev-surface-2); aspect-ratio: 4/3; }
        .evf-dia img { width: 100%; height: 100%; object-fit: cover; display: block; }
        .evf-dia .vacio { position: absolute; inset: 0; display: grid; place-items: center; color: var(--ev-muted); font-size: 24px; }
        .evf-dia .n { position: absolute; left: 6px; top: 6px; right: 6px; z-index: 2; font: 800 11.5px var(--ev-font); color: #fff; background: rgba(11,18,21,.75); border-radius: 8px; padding: 4px 8px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .evf-dia .acc { position: absolute; right: 6px; bottom: 6px; z-index: 2; display: flex; gap: 4px; }
        .evf-dia .acc label, .evf-dia .acc button { width: 30px; height: 30px; border-radius: 9px; border: 0; cursor: pointer; display: grid; place-items: center; font-size: 13px; background: rgba(11,18,21,.75); color: #fff; }
        .evf-dia .acc label:hover, .evf-dia .acc button:hover { background: #0B1215; }
        .evf-dia .acc .peligro:hover { background: #B42318; }
        .evf-dia.subiendo::after { content: "Subiendo…"; position: absolute; inset: 0; z-index: 3; display: grid; place-items: center; font: 800 12px var(--ev-font); background: rgba(255,255,255,.75); }
        .evf-dia.sobre { outline: 3px solid var(--ev-teal); }`;
        document.head.appendChild(st);
    }

    /** Fotos de los días del itinerario de un circuito (se guardan al momento en circuito_itinerario). */
    function montarDias(el, circuito, alCambiar) {
        let dias = [];
        const pintar = () => {
            el.innerHTML = `<div class="evf-dias"><h4><i class="bi bi-signpost-2"></i> Fotos del itinerario <small>una por día · se guardan al momento</small></h4>
                ${dias.length ? `<div class="evf-lista">${dias.map(d => `<div class="evf-dia" data-dia="${d.dia}" title="Arrastra una foto aquí">
                    ${d.caratula ? `<img src="${esc(d.caratula)}" alt="" loading="lazy" onerror="this.remove()">` : '<span class="vacio"><i class="bi bi-image"></i></span>'}
                    <span class="n">Día ${d.dia}${d.titulo ? ' · ' + esc(d.titulo) : ''}</span>
                    <span class="acc"><label title="${d.caratula ? 'Cambiar' : 'Subir'} foto del día ${d.dia}"><i class="bi bi-upload"></i><input type="file" accept="image/jpeg,image/png,image/webp" hidden data-subir-dia="${d.dia}"></label>
                        ${d.caratula ? `<button type="button" class="peligro" data-quitar-dia="${d.dia}" title="Quitar foto del día ${d.dia}"><i class="bi bi-trash3"></i></button>` : ''}</span></div>`).join('')}</div>`
                  : '<div class="mm-aviso">Este circuito todavía no tiene días en el itinerario: créalos en Circuitos → Itinerario.</div>'}</div>`;
        };
        const cargar = async () => {
            el.innerHTML = '<div class="ui-skel" style="height:120px;border-radius:18px"></div>';
            try { dias = await EV.api('/Circuitos/Dias?nombre=' + encodeURIComponent(circuito)); pintar(); }
            catch (e) { el.innerHTML = `<div class="evf-dias"><div class="mm-aviso">${esc(e.message)}</div></div>`; }
        };
        const subir = async (dia, archivo) => {
            if (!TIPOS.test(archivo.type) || archivo.size > 10_000_000) { EV.toast('Usa una imagen JPG, PNG o WebP de hasta 10 MB.', 'err'); return; }
            const caja = el.querySelector(`[data-dia="${dia}"]`); caja?.classList.add('subiendo');
            const fd = new FormData(); fd.append('nombre', circuito); fd.append('dia', dia); fd.append('archivo', archivo);
            try { const r = await EVProgreso.subir('/Circuitos/FotoDia', fd); EV.toast(r.mensaje); await cargar(); alCambiar?.(); }
            catch (e) { EV.toast(e.message, 'err'); caja?.classList.remove('subiendo'); }
        };
        el.addEventListener('change', e => { const i = e.target.closest('[data-subir-dia]'); if (i?.files[0]) subir(+i.dataset.subirDia, i.files[0]); });
        el.addEventListener('click', async e => {
            const q = e.target.closest('[data-quitar-dia]'); if (!q) return;
            if (!await EV.confirmar({ titulo: `¿Quitar la foto del día ${q.dataset.quitarDia}?`, peligro: true, si: 'Sí, quitar' })) return;
            try { const r = await EV.post(`/Circuitos/QuitarFotoDia?nombre=${encodeURIComponent(circuito)}&dia=${q.dataset.quitarDia}`); EV.toast(r.mensaje); await cargar(); alCambiar?.(); }
            catch (err) { EV.toast(err.message, 'err'); }
        });
        el.addEventListener('dragover', e => { const z = e.target.closest('[data-dia]'); if (z) { e.preventDefault(); z.classList.add('sobre'); } });
        el.addEventListener('dragleave', e => e.target.closest('[data-dia]')?.classList.remove('sobre'));
        el.addEventListener('drop', e => { const z = e.target.closest('[data-dia]'); if (!z) return; e.preventDefault(); z.classList.remove('sobre'); if (e.dataTransfer.files[0]) subir(+z.dataset.dia, e.dataTransfer.files[0]); });
        cargar();
    }

    function montar(el, o) {
        estilos();
        const c = CFG[o.tipo] || { nombre: 'el producto' };
        el.innerHTML = `<div class="evf">${c.ayuda ? `<div class="evf-ayuda"><i class="bi bi-info-circle"></i> ${esc(c.ayuda)}</div>` : ''}<div data-evf-mm></div>${c.dias ? '<div data-evf-dias></div>' : ''}</div>`;
        // Las fotos se guardan solas: que no cuenten como "cambios sin guardar" del panel donde están
        ['input', 'change'].forEach(ev => el.addEventListener(ev, e => e.stopPropagation()));
        const g = EVMultimedia.montar(el.querySelector('[data-evf-mm]'), {
            tipo: o.tipo, clave: o.clave, portada: true, intermedia: !!c.intermedia, galeria: c.galeria !== false, nombre: c.nombre, alCambiar: o.alCambiar
        });
        if (c.dias) montarDias(el.querySelector('[data-evf-dias]'), String(o.clave), () => o.alCambiar?.(null));
        return g;
    }

    let panel = null, dPanel = null;
    function abrir(o) {
        estilos();
        if (!panel) {
            panel = document.createElement('aside');
            panel.className = 'ui-drawer'; panel.setAttribute('aria-hidden', 'true');
            panel.innerHTML = `<div class="ui-dh"><div class="ev-module-icon" style="width:46px;height:46px"><i class="bi bi-images"></i></div>
                    <div style="min-width:0"><h2 data-t></h2><small data-s></small></div>
                    <button class="ui-ibtn x" type="button" data-cerrar aria-label="Cerrar"><i class="bi bi-x-lg"></i></button></div>
                <div class="ui-db" data-cuerpo></div>
                <div class="ui-df"><a class="ev-btn ghost" data-modulo hidden></a><span class="hint">Las fotos se guardan al momento</span><button class="ev-btn" type="button" data-cerrar>Listo</button></div>`;
            document.body.appendChild(panel);
            dPanel = EV.drawer(panel);
            panel.querySelectorAll('[data-cerrar]').forEach(b => b.addEventListener('click', () => dPanel.cerrar(true)));
            document.addEventListener('keydown', e => { if (e.key === 'Escape' && dPanel.abierto() && !document.querySelector('.ui-modal.open')) dPanel.cerrar(true); });
        }
        const c = CFG[o.tipo] || {};
        panel.querySelector('[data-t]').textContent = o.titulo || 'Fotos';
        panel.querySelector('[data-s]').textContent = `Fotos de ${c.nombre || 'el producto'}${c.dias ? ' y de su itinerario' : ''}`;
        const m = panel.querySelector('[data-modulo]');
        m.hidden = !(o.modulo !== false && c.modulo && !location.pathname.startsWith(c.modulo[1].replace('/Index', '')));
        if (!m.hidden) { m.href = c.modulo[1]; m.innerHTML = `<i class="bi bi-box-arrow-up-right"></i> Abrir en ${c.modulo[0]}`; }
        montar(panel.querySelector('[data-cuerpo]'), { tipo: o.tipo, clave: o.clave, alCambiar: o.alCambiar });
        dPanel.abrir();
    }

    return { montar, abrir };
})();
