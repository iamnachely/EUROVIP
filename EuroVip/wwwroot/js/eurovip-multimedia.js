// =====================================================================================================
// EVMultimedia — fotos de un producto (Eurovip.imagen + Eurovip.multimedia, documento esquema-multimedia.md)
//   const g = EVMultimedia.montar(elemento, { tipo: 'EXCURSION', clave: 37, portada: true, intermedia: false, galeria: true,
//                                            nombre: 'la excursión', alCambiar: items => {} });
//   g.cambiar(clave)   -> muestra las fotos de otro producto (null = "guarda primero")
//   g.recargar()
// Portada (una), intermedia (una, solo circuitos) y galería (varias, por orden). Cada foto se guarda al momento:
// no depende del botón Guardar de la pantalla. Usa EV (eurovip-ui.js) y EVProgreso (eurovip-progreso.js).
// =====================================================================================================
window.EVMultimedia = (() => {
    const esc = s => (s ?? '').toString().replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const kb = b => b == null ? '' : b < 1048576 ? `${Math.round(b / 1024)} KB` : `${(b / 1048576).toFixed(1)} MB`;
    const TIPOS = /^image\/(jpeg|png|webp)$/;

    function estilos() {
        if (document.getElementById('evmm-css')) return;
        const st = document.createElement('style');
        st.id = 'evmm-css';
        st.textContent = `
        .mm { border: 1px solid var(--ev-line); border-radius: 18px; padding: 14px; background: var(--ev-surface); }
        .mm-h { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; margin-bottom: 12px; }
        .mm-h b { font-size: 14.5px; } .mm-h small { color: var(--ev-muted); font-weight: 600; } .mm-h .sp { flex: 1; }
        .mm-aviso { color: var(--ev-muted); font-weight: 600; font-size: 13px; padding: 14px; border-radius: 14px; background: var(--ev-surface-2); text-align: center; }
        .mm-slots { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 12px; margin-bottom: 12px; }
        .mm-slot { position: relative; border-radius: 14px; border: 2px dashed var(--ev-line); background: var(--ev-surface-2); aspect-ratio: 16/10; overflow: hidden; display: grid; place-items: center; }
        .mm-slot.lleno { border-style: solid; }
        .mm-slot img, .mm-th img { width: 100%; height: 100%; object-fit: cover; display: block; }
        .mm-slot .vacio { text-align: center; color: var(--ev-muted); font-weight: 700; font-size: 12.5px; padding: 10px; }
        .mm-slot .vacio i { font-size: 26px; display: block; margin-bottom: 4px; }
        .mm-et { position: absolute; left: 8px; top: 8px; z-index: 2; font: 800 11px var(--ev-font); padding: 4px 8px; border-radius: 8px; background: var(--ev-ink); color: var(--ev-aqua); }
        .mm-acc { position: absolute; right: 6px; bottom: 6px; z-index: 2; display: flex; gap: 4px; flex-wrap: wrap; justify-content: flex-end; }
        .mm-acc button, .mm-acc label { width: 30px; height: 30px; border-radius: 9px; border: 0; cursor: pointer; display: grid; place-items: center; font-size: 13px;
                                        background: rgba(11,18,21,.75); color: #fff; backdrop-filter: blur(4px); }
        .mm-acc button:hover, .mm-acc label:hover { background: #0B1215; }
        .mm-acc button:disabled { opacity: .35; cursor: default; }
        .mm-acc .peligro:hover { background: #B42318; }
        .mm-gal { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; min-height: 60px; border-radius: 14px; }
        .mm-th { position: relative; border-radius: 12px; overflow: hidden; aspect-ratio: 4/3; background: var(--ev-surface-2); border: 1px solid var(--ev-line); }
        .mm-th[draggable="true"] { cursor: grab; } .mm-th.arr { opacity: .35; } .mm-th.sobre { outline: 3px solid var(--ev-teal); }
        .mm-th .n { position: absolute; left: 6px; top: 6px; z-index: 2; min-width: 24px; height: 24px; border-radius: 8px; background: rgba(11,18,21,.75); color: #fff; font: 800 12px var(--ev-font); display: grid; place-items: center; padding: 0 6px; }
        .mm-th .alt { position: absolute; left: 0; right: 0; bottom: 0; padding: 22px 8px 40px; font: 600 11px var(--ev-font); color: #fff; background: linear-gradient(transparent, rgba(0,0,0,.65)); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; pointer-events: none; }
        .mm-falta { position: absolute; inset: 0; display: grid; place-items: center; text-align: center; padding: 8px; font: 700 11.5px var(--ev-font); color: #93370D; background: #FFFAEB; }
        .mm-add { border: 2px dashed var(--ev-line); border-radius: 12px; aspect-ratio: 4/3; display: grid; place-items: center; text-align: center; cursor: pointer; color: var(--ev-muted); font: 700 12.5px var(--ev-font); background: transparent; }
        .mm-add:hover, .mm-sobre { border-color: var(--ev-teal) !important; background: var(--ev-mint) !important; color: var(--ev-deep); }
        .mm-add i { font-size: 24px; display: block; margin-bottom: 4px; }
        .mm-barra { height: 6px; border-radius: 9px; background: var(--ev-surface-2); overflow: hidden; margin: -4px 0 10px; }
        .mm-barra i { display: block; height: 100%; width: 0; background: var(--ev-teal); transition: width .2s; }
        .mm-pie { color: var(--ev-muted); font-size: 12px; font-weight: 600; margin-top: 10px; }`;
        document.head.appendChild(st);
    }

    function montar(el, op) {
        estilos();
        const o = Object.assign({ portada: true, intermedia: false, galeria: true, nombre: 'el producto', alCambiar: null }, op);
        let clave = o.clave ?? null, items = [], arrastre = null;

        const de = uso => items.filter(x => x.uso === uso).sort((a, b) => a.orden - b.orden);
        const imagenDe = x => x.existe ? `<img src="${esc(x.url)}" alt="${esc(x.texto_Alternativo || '')}" loading="lazy">`
                                       : '<span class="mm-falta">El archivo no está en este servidor</span>';

        function slot(uso, titulo) {
            const x = de(uso)[0];
            return `<div class="mm-slot ${x ? 'lleno' : ''}" data-soltar="${uso}" title="Arrastra una foto aquí">
                <span class="mm-et">${titulo}</span>
                ${x ? imagenDe(x) : `<span class="vacio"><i class="bi bi-image"></i>Sin ${titulo.toLowerCase()}<br><small>Arrastra una foto o pulsa subir</small></span>`}
                <span class="mm-acc">
                    <label title="${x ? 'Cambiar' : 'Subir'} ${titulo.toLowerCase()}"><i class="bi bi-upload"></i><input type="file" accept="image/jpeg,image/png,image/webp" data-subir="${uso}" hidden></label>
                    ${x ? `<button type="button" data-texto="${x.id}" title="Descripción de la foto (para Google y lectores de pantalla)"><i class="bi bi-chat-left-text"></i></button>
                           <button type="button" class="peligro" data-quitar="${x.id}" title="Quitar ${titulo.toLowerCase()}"><i class="bi bi-trash3"></i></button>` : ''}
                </span></div>`;
        }

        function pintar() {
            if (clave == null || clave === '') {
                el.innerHTML = `<div class="mm"><div class="mm-aviso"><i class="bi bi-info-circle"></i> Guarda primero ${esc(o.nombre)} para añadir sus fotos.</div></div>`;
                return;
            }
            const gal = de('GALERIA');
            el.innerHTML = `<div class="mm">
                <div class="mm-h"><i class="bi bi-images" style="color:var(--ev-deep)"></i><b>Fotos</b><small>${items.length} en total</small><span class="sp"></span>
                    ${o.galeria ? `<label class="ev-btn ghost sm" style="cursor:pointer"><i class="bi bi-plus-lg"></i> Añadir a la galería<input type="file" accept="image/jpeg,image/png,image/webp" multiple data-subir="GALERIA" hidden></label>` : ''}</div>
                <div class="mm-barra" hidden><i></i></div>
                ${o.portada || o.intermedia ? `<div class="mm-slots">${o.portada ? slot('PORTADA', 'Portada') : ''}${o.intermedia ? slot('INTERMEDIA', 'Intermedia') : ''}</div>` : ''}
                ${o.galeria ? `<div class="mm-gal" data-soltar="GALERIA">
                    ${gal.map((x, i) => `<div class="mm-th" draggable="true" data-id="${x.id}">
                        <span class="n">${i + 1}</span>${imagenDe(x)}${x.texto_Alternativo ? `<span class="alt">${esc(x.texto_Alternativo)}</span>` : ''}
                        <span class="mm-acc">
                            <button type="button" data-mover="${x.id}" data-paso="-1" ${i === 0 ? 'disabled' : ''} title="Mover antes"><i class="bi bi-chevron-left"></i></button>
                            <button type="button" data-mover="${x.id}" data-paso="1" ${i === gal.length - 1 ? 'disabled' : ''} title="Mover después"><i class="bi bi-chevron-right"></i></button>
                            ${o.portada ? `<button type="button" data-portada="${x.id}" title="Usar como portada"><i class="bi bi-star"></i></button>` : ''}
                            <button type="button" data-texto="${x.id}" title="Descripción de la foto"><i class="bi bi-chat-left-text"></i></button>
                            <button type="button" class="peligro" data-quitar="${x.id}" title="Quitar de la galería"><i class="bi bi-trash3"></i></button>
                        </span></div>`).join('')}
                    <label class="mm-add"><span><i class="bi bi-cloud-arrow-up"></i>Arrastra fotos aquí<br>o pulsa para elegir</span><input type="file" accept="image/jpeg,image/png,image/webp" multiple data-subir="GALERIA" hidden></label>
                </div>` : ''}
                <div class="mm-pie">JPG, PNG o WebP hasta 10 MB; se guardan en WebP de 1600 px como máximo.${o.galeria ? ' Arrastra las fotos de la galería o usa las flechas para cambiar el orden.' : ''}</div>
            </div>`;
        }

        async function cargar() {
            if (clave == null || clave === '') { items = []; pintar(); return; }
            el.innerHTML = '<div class="mm"><div class="ui-skel" style="height:120px"></div></div>';
            try { items = (await EV.api(`/Multimedia/Listar?tipo=${encodeURIComponent(o.tipo)}&clave=${encodeURIComponent(clave)}`)).items; }
            catch (e) { el.innerHTML = `<div class="mm"><div class="mm-aviso">${esc(e.message)}</div></div>`; return; }
            pintar();
        }

        async function cambiado() { await cargar(); o.alCambiar?.(items); }

        async function subir(uso, archivos) {
            const lista = [...archivos].filter(f => TIPOS.test(f.type));
            if (!lista.length) { EV.toast('Usa imágenes JPG, PNG o WebP.', 'err'); return; }
            if (lista.some(f => f.size > 10_000_000)) { EV.toast('Alguna imagen pesa más de 10 MB.', 'err'); return; }
            const fd = new FormData();
            fd.append('tipo', o.tipo); fd.append('clave', clave); fd.append('uso', uso);
            (uso === 'GALERIA' ? lista : lista.slice(0, 1)).forEach(f => fd.append('archivos', f));
            const barra = el.querySelector('.mm-barra');
            if (barra) barra.hidden = false;
            try {
                const r = await EVProgreso.subir('/Multimedia/Subir', fd, p => { if (barra) barra.firstElementChild.style.width = Math.min(100, p) + '%'; });
                EV.toast(r.mensaje, r.errores?.length ? 'err' : 'ok');
                await cambiado();
            } catch (e) { EV.toast(e.message, 'err'); if (barra) barra.hidden = true; }
        }

        async function mover(id, paso) {
            const ids = de('GALERIA').map(x => x.id), i = ids.indexOf(id), j = i + paso;
            if (i < 0 || j < 0 || j >= ids.length) return;
            [ids[i], ids[j]] = [ids[j], ids[i]];
            await ordenar(ids);
        }

        async function ordenar(ids) {
            try { await EV.post('/Multimedia/Ordenar', { Tipo: o.tipo, Clave: String(clave), Ids: ids }); await cambiado(); }
            catch (e) { EV.toast(e.message, 'err'); }
        }

        el.addEventListener('change', e => {
            const inp = e.target.closest('[data-subir]'); if (!inp) return;
            const f = inp.files; if (f?.length) subir(inp.dataset.subir, f); inp.value = '';
        });
        el.addEventListener('click', async e => {
            const q = e.target.closest('[data-quitar]'), p = e.target.closest('[data-portada]'), t = e.target.closest('[data-texto]'), m = e.target.closest('[data-mover]');
            if (q) {
                e.preventDefault();
                if (!await EV.confirmar({ titulo: '¿Quitar la foto?', texto: 'Se borra de este producto. No se puede deshacer.', si: 'Sí, quitar', peligro: true })) return;
                try { const r = await EV.post('/Multimedia/Quitar?id=' + q.dataset.quitar); EV.toast(r.mensaje); await cambiado(); } catch (er) { EV.toast(er.message, 'err'); }
            } else if (p) {
                e.preventDefault();
                try { const r = await EV.post('/Multimedia/Portada?id=' + p.dataset.portada); EV.toast(r.mensaje); await cambiado(); } catch (er) { EV.toast(er.message, 'err'); }
            } else if (t) {
                e.preventDefault();
                const x = items.find(i => i.id === +t.dataset.texto); if (!x) return;
                const v = await EV.pedir({ titulo: 'Descripción de la foto', etiqueta: 'Qué se ve en la foto', valor: x.texto_Alternativo || '', maxlength: 250,
                                           ayuda: 'Ej. «Piscina principal del hotel». Ayuda a Google y a quien usa lector de pantalla.' });
                if (v == null) return;
                try { const r = await EV.post('/Multimedia/Texto', { Id: x.id, Texto: v.trim() || null }); EV.toast(r.mensaje); await cambiado(); } catch (er) { EV.toast(er.message, 'err'); }
            } else if (m) { e.preventDefault(); mover(+m.dataset.mover, +m.dataset.paso); }
        });
        // Arrastrar archivos a la portada / intermedia / galería, y arrastrar fotos de la galería para ordenarlas
        el.addEventListener('dragstart', e => { const th = e.target.closest('.mm-th'); if (!th) return; arrastre = th; th.classList.add('arr'); e.dataTransfer.effectAllowed = 'move'; });
        el.addEventListener('dragend', () => { arrastre?.classList.remove('arr'); arrastre = null; el.querySelectorAll('.sobre,.mm-sobre').forEach(x => x.classList.remove('sobre', 'mm-sobre')); });
        el.addEventListener('dragover', e => {
            if (arrastre) { const th = e.target.closest('.mm-th'); if (th && th !== arrastre) { e.preventDefault(); el.querySelectorAll('.mm-th.sobre').forEach(x => x.classList.remove('sobre')); th.classList.add('sobre'); } return; }
            const z = e.target.closest('[data-soltar]'); if (!z || !e.dataTransfer.types.includes('Files')) return;
            e.preventDefault(); z.classList.add('mm-sobre');
        });
        el.addEventListener('dragleave', e => e.target.closest('[data-soltar]')?.classList.remove('mm-sobre'));
        el.addEventListener('drop', async e => {
            if (arrastre) {
                const th = e.target.closest('.mm-th'); if (!th || th === arrastre) return;
                e.preventDefault();
                const ids = de('GALERIA').map(x => x.id), de1 = ids.indexOf(+arrastre.dataset.id), a = ids.indexOf(+th.dataset.id);
                ids.splice(a, 0, ids.splice(de1, 1)[0]);
                await ordenar(ids);
                return;
            }
            const z = e.target.closest('[data-soltar]'); if (!z) return;
            e.preventDefault(); z.classList.remove('mm-sobre');
            if (e.dataTransfer.files.length) subir(z.dataset.soltar, e.dataTransfer.files);
        });

        cargar();
        return {
            cambiar: c => { clave = c ?? null; return cargar(); },
            recargar: cargar,
            get items() { return items; }
        };
    }

    return { montar };
})();
