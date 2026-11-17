// Circuitos › «Circuitos por el mundo» (CAMBIO 133). Pedido de la usuaria: «los circuitos de la integración (Yourttoo) se
// eligen visualmente, agrupados por destino, y se guardan en la tabla inicio para la Home; nada de formularios».
//   • Arriba: lo que ya está en la Home, por destino (se ordena con las flechas y se quita con ×).
//   • Izquierda: los destinos de Yourttoo (con buscador). Los que ya tienen circuitos elegidos salen primero.
//   • Derecha: los circuitos del destino, del más económico al más caro, en tarjetas. Un clic los agrega o los quita.
// Usa EV (eurovip-ui.js) para llamar al servidor. Las rutas están en CircuitosController (Mundo*).
window.CXM = (() => {
    const $ = (s, el = document) => el.querySelector(s);
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const dinero = (v, m) => v == null ? '—' : `${m === 'EUR' ? '€' : m === 'DOP' ? 'RD$' : 'US$'}${Number(v).toLocaleString('es-DO', { maximumFractionDigits: 0 })}`;
    let raiz, destinos = [], elegidos = [], actual = null, pagina = null, cargando = false, filtro = '';

    function estilos() {
        if ($('#cxm-css')) return;
        const s = document.createElement('style'); s.id = 'cxm-css';
        s.textContent = `
.cxm-home{border:1px solid var(--ev-line,#E2EFED);border-radius:20px;padding:16px 18px;margin-bottom:16px;background:var(--ev-surface,#fff)}
.cxm-home h3{margin:0 0 4px;font-size:16px;font-weight:800;display:flex;gap:8px;align-items:center}
.cxm-home .sub{color:var(--ev-muted,#66807C);font-weight:600;font-size:13px;margin:0 0 10px}
.cxm-grupo{margin-top:10px}.cxm-grupo>b{font-size:13px;text-transform:uppercase;letter-spacing:.04em;color:var(--ev-muted,#66807C)}
.cxm-fila{display:flex;gap:10px;overflow:auto;padding:8px 2px 4px}
.cxm-mini{flex:0 0 220px;display:flex;gap:10px;align-items:center;border:1px solid var(--ev-line,#E2EFED);border-radius:14px;padding:8px;background:var(--ev-surface-2,#F5FBFA)}
.cxm-mini img,.cxm-mini .ph{width:56px;height:56px;border-radius:10px;object-fit:cover;flex:none;background:#DDEFEC}
.cxm-mini .t{font-weight:700;font-size:12.5px;line-height:1.25;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden}
.cxm-mini .p{font-size:12px;color:#C2410C;font-weight:800}
.cxm-mini .acc{display:flex;flex-direction:column;gap:2px;margin-left:auto}
.cxm-mini .acc button{border:0;background:none;cursor:pointer;color:var(--ev-muted,#66807C);padding:2px;font-size:13px}
.cxm-mini .acc button:hover{color:var(--ev-ink,#0B1215)}
.cxm-cuerpo{display:grid;grid-template-columns:260px 1fr;gap:16px;align-items:start}
@media (max-width:860px){.cxm-cuerpo{grid-template-columns:1fr}}
.cxm-dest{border:1px solid var(--ev-line,#E2EFED);border-radius:18px;padding:10px;background:var(--ev-surface,#fff);position:sticky;top:12px;max-height:78vh;display:flex;flex-direction:column}
.cxm-dest input{width:100%;border:1px solid var(--ev-line,#E2EFED);border-radius:12px;padding:8px 10px;margin-bottom:8px;font:inherit}
.cxm-dest ul{list-style:none;margin:0;padding:0;overflow:auto}
.cxm-dest li button{width:100%;display:flex;justify-content:space-between;align-items:center;border:0;background:none;padding:8px 10px;border-radius:10px;cursor:pointer;font:inherit;font-weight:600;text-align:left;color:inherit}
.cxm-dest li button:hover{background:var(--ev-surface-2,#F5FBFA)} .cxm-dest li button.on{background:var(--ev-ink,#0B1215);color:#fff}
.cxm-dest .n{font-size:11.5px;font-weight:800;border-radius:999px;padding:1px 8px;background:#DCFCE7;color:#166534}
.cxm-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:14px}
.cxm-card{border:1px solid var(--ev-line,#E2EFED);border-radius:18px;overflow:hidden;background:var(--ev-surface,#fff);display:flex;flex-direction:column;transition:box-shadow .15s,border-color .15s}
.cxm-card.si{border-color:#16A34A;box-shadow:0 0 0 3px rgba(22,163,74,.18)}
.cxm-card .ft{position:relative;height:150px;background:#DDEFEC}
.cxm-card .ft img{width:100%;height:100%;object-fit:cover;display:block}
.cxm-card .dias{position:absolute;left:10px;bottom:10px;background:#EA580C;color:#fff;font-weight:800;font-size:12px;border-radius:999px;padding:3px 10px}
.cxm-card .ok{position:absolute;right:10px;top:10px;background:#16A34A;color:#fff;font-weight:800;font-size:12px;border-radius:999px;padding:3px 10px}
.cxm-card .bd{padding:12px 14px;display:flex;flex-direction:column;gap:4px;flex:1}
.cxm-card h4{margin:0;font-size:14.5px;font-weight:800;line-height:1.3}
.cxm-card small{color:var(--ev-muted,#66807C);font-weight:600}
.cxm-card .pr{margin-top:auto;padding-top:6px;font-weight:600;font-size:13px}.cxm-card .pr b{color:#C2410C;font-size:17px}
.cxm-card button.sel{margin:0 14px 14px;border-radius:999px;padding:9px 14px;font-weight:800;cursor:pointer;border:1px solid var(--ev-line,#E2EFED);background:var(--ev-ink,#0B1215);color:#fff;font:inherit;font-weight:800}
.cxm-card.si button.sel{background:#fff;color:#B42318;border-color:#FECDCA}
.cxm-vacio{border:1px dashed var(--ev-line,#E2EFED);border-radius:18px;padding:30px;text-align:center;color:var(--ev-muted,#66807C);font-weight:600}`;
        document.head.appendChild(s);
    }

    async function iniciar() {
        estilos();
        raiz = $('#cxMundo');
        raiz.innerHTML = `<div class="cxm-home" id="cxmHome"></div>
            <div class="cxm-cuerpo"><aside class="cxm-dest"><input type="search" id="cxmFiltro" placeholder="Buscar destino (Turquía, Perú…)"><ul id="cxmDest"><li class="cxm-vacio">Cargando destinos de Yourttoo…</li></ul></aside>
            <section id="cxmProgs"><div class="cxm-vacio"><i class="bi bi-globe-americas" style="font-size:28px;display:block;margin-bottom:6px"></i>Elige un destino a la izquierda para ver sus circuitos, del más económico al más caro.</div></section></div>`;
        $('#cxmFiltro').addEventListener('input', e => { filtro = e.target.value.trim().toLowerCase(); pintarDestinos(); });
        await Promise.all([cargarElegidos(), cargarDestinos()]);
    }

    async function cargarDestinos() {
        try { destinos = await EV.api('/Circuitos/MundoDestinos'); pintarDestinos(); }
        catch (e) { $('#cxmDest').innerHTML = `<li class="cxm-vacio">No pude conectar con Yourttoo: ${esc(e.message)}</li>`; }
    }
    async function cargarElegidos() {
        try { elegidos = await EV.api('/Circuitos/MundoElegidos'); } catch (e) { elegidos = []; EV.toast(e.message, 'err'); }
        pintarHome();
    }

    function pintarDestinos() {
        const norm = s => s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
        const cuenta = new Map(); elegidos.forEach(x => cuenta.set(x.pais, (cuenta.get(x.pais) || 0) + 1));
        const l = destinos.filter(d => !filtro || norm(d.nombre).includes(norm(filtro)) || d.pais === filtro)
            .sort((a, b) => (cuenta.get(b.pais) || 0) - (cuenta.get(a.pais) || 0) || a.nombre.localeCompare(b.nombre, 'es'));
        $('#cxmDest').innerHTML = l.map(d => `<li><button type="button" data-pais="${esc(d.pais)}" class="${actual === d.pais ? 'on' : ''}"><span>${esc(d.nombre)}</span>${cuenta.get(d.pais) ? `<span class="n" title="Circuitos de este destino en la Home">${cuenta.get(d.pais)}</span>` : ''}</button></li>`).join('')
            || '<li class="cxm-vacio">Ningún destino con ese nombre.</li>';
        $('#cxmDest').querySelectorAll('[data-pais]').forEach(b => b.onclick = () => abrir(b.dataset.pais));
    }

    function pintarHome() {
        const grupos = new Map(); elegidos.forEach(x => { if (!grupos.has(x.pais)) grupos.set(x.pais, { nombre: x.destino, items: [] }); grupos.get(x.pais).items.push(x); });
        $('#cxmHome').innerHTML = `<h3><i class="bi bi-house-heart"></i> En la Home <small style="font-weight:600;color:var(--ev-muted)">${elegidos.length} circuito${elegidos.length === 1 ? '' : 's'} en ${grupos.size} destino${grupos.size === 1 ? '' : 's'}</small></h3>
            <p class="sub">Así se agrupan en la Home. Ordénalos con las flechas (el primero sale primero) o quítalos con ×.</p>
            ${grupos.size ? [...grupos.entries()].map(([pais, g]) => `<div class="cxm-grupo"><b><i class="bi bi-geo-alt-fill"></i> ${esc(g.nombre)}</b>
                <div class="cxm-fila">${g.items.map((x, i) => `<div class="cxm-mini" title="${esc(x.titulo)}">
                    ${x.imagen ? `<img src="${esc(x.imagen)}" alt="" loading="lazy" onerror="this.replaceWith(Object.assign(document.createElement('span'),{className:'ph'}))">` : '<span class="ph"></span>'}
                    <div style="min-width:0"><div class="t">${esc(x.titulo)}</div><div class="p">Desde ${dinero(x.desde, x.moneda)}</div><small style="color:var(--ev-muted);font-weight:600">${x.dias ? x.dias + ' días' : ''}</small></div>
                    <div class="acc"><button type="button" title="Antes" data-mover="${pais}|${i}|-1" ${i === 0 ? 'disabled' : ''}><i class="bi bi-arrow-left"></i></button>
                        <button type="button" title="Quitar de la Home" data-quitar="${x.id}"><i class="bi bi-x-lg"></i></button>
                        <button type="button" title="Después" data-mover="${pais}|${i}|1" ${i === g.items.length - 1 ? 'disabled' : ''}><i class="bi bi-arrow-right"></i></button></div></div>`).join('')}</div></div>`).join('')
              : '<div class="cxm-vacio">Todavía no hay circuitos por el mundo en la Home. Elige un destino y pulsa «Agregar a la Home» en los que quieras.</div>'}`;
        $('#cxmHome').querySelectorAll('[data-quitar]').forEach(b => b.onclick = () => quitar(+b.dataset.quitar));
        $('#cxmHome').querySelectorAll('[data-mover]').forEach(b => b.onclick = () => { const [p, i, d] = b.dataset.mover.split('|'); mover(p, +i, +d); });
    }

    async function abrir(pais, pag = 0) {
        if (cargando) return;
        actual = pais; pintarDestinos();
        const caja = $('#cxmProgs');
        if (pag === 0) caja.innerHTML = '<div class="cxm-vacio"><span class="ui-spin"></span> Buscando los circuitos en Yourttoo…</div>';
        cargando = true;
        try {
            const r = await EV.api(`/Circuitos/MundoProgramas?pais=${encodeURIComponent(pais)}&pagina=${pag}`);
            pagina = pag === 0 ? r : { ...r, programas: [...pagina.programas, ...r.programas] };
            pintarProgramas();
        } catch (e) { caja.innerHTML = `<div class="cxm-vacio">No pude traer los circuitos: ${esc(e.message)}<br><button type="button" class="ev-btn sm" style="margin-top:10px" onclick="CXM.abrir('${esc(pais)}')">Reintentar</button></div>`; }
        finally { cargando = false; }
    }

    function pintarProgramas() {
        const p = pagina, caja = $('#cxmProgs');
        const ids = new Map(elegidos.map(x => [x.codigo, x.id]));
        caja.innerHTML = `<div style="display:flex;align-items:baseline;gap:10px;margin:0 0 12px;flex-wrap:wrap"><h3 style="margin:0;font-size:19px;font-weight:800">${esc(p.nombre)}</h3>
                <small style="color:var(--ev-muted);font-weight:600">${p.total} circuito${p.total === 1 ? '' : 's'} · del más económico al más caro · precio de Yourttoo por persona en doble (la web aplica el de la agencia)</small></div>
            ${p.programas.length ? `<div class="cxm-grid">${p.programas.map(x => { const id = ids.get(x.codigo); return `<article class="cxm-card ${id ? 'si' : ''}">
                <div class="ft">${x.imagen ? `<img src="${esc(x.imagen)}" alt="" loading="lazy" onerror="this.remove()">` : ''}${x.dias ? `<span class="dias">${x.dias} días</span>` : ''}${id ? '<span class="ok"><i class="bi bi-check2"></i> En la Home</span>' : ''}</div>
                <div class="bd"><h4>${esc(x.titulo)}</h4>${x.ciudades ? `<small>${esc(x.ciudades)}</small>` : ''}${x.categoria ? `<small>${esc(x.categoria)}</small>` : ''}
                    <div class="pr">Desde <b>${dinero(x.desde, x.moneda)}</b><br><small>por persona en doble · ${esc(x.codigo)}</small></div></div>
                <button type="button" class="sel" data-codigo="${esc(x.codigo)}" data-id="${id ?? ''}">${id ? '<i class="bi bi-x-lg"></i> Quitar de la Home' : '<i class="bi bi-plus-lg"></i> Agregar a la Home'}</button></article>`; }).join('')}</div>`
              : '<div class="cxm-vacio">Yourttoo no tiene circuitos en este destino.</div>'}
            ${p.actual + 1 < p.paginas ? `<div style="text-align:center;margin-top:14px"><button type="button" class="ev-btn ghost" id="cxmMas"><i class="bi bi-chevron-down"></i> Ver más (${p.programas.length} de ${p.total})</button></div>` : ''}`;
        caja.querySelectorAll('button.sel').forEach(b => b.onclick = () => b.dataset.id ? quitar(+b.dataset.id) : agregar(b.dataset.codigo, b));
        $('#cxmMas')?.addEventListener('click', () => abrir(p.pais, p.actual + 1));
    }

    async function agregar(codigo, boton) {
        if (boton) { boton.disabled = true; boton.innerHTML = '<span class="ui-spin"></span> Agregando…'; }
        try { const r = await EV.post('/Circuitos/MundoAgregar', { Codigo: codigo, Pais: actual }); EV.toast(r.mensaje, 'ok'); }
        catch (e) { EV.toast(e.message, 'err'); }
        await cargarElegidos(); pintarDestinos(); if (pagina) pintarProgramas();
    }
    async function quitar(id) {
        try { const r = await EV.post('/Circuitos/MundoQuitar?id=' + id, {}); EV.toast(r.mensaje, 'ok'); }
        catch (e) { EV.toast(e.message, 'err'); }
        await cargarElegidos(); pintarDestinos(); if (pagina) pintarProgramas();
    }
    async function mover(pais, i, d) {
        const l = elegidos.filter(x => x.pais === pais).map(x => x.id), j = i + d;
        if (j < 0 || j >= l.length) return;
        [l[i], l[j]] = [l[j], l[i]];
        try { await EV.post('/Circuitos/MundoOrdenar', { Pais: pais, Ids: l }); } catch (e) { EV.toast(e.message, 'err'); }
        await cargarElegidos();
    }

    // Pestañas «Mis circuitos» / «Circuitos por el mundo»: lo de Yourttoo se carga solo la primera vez que se abre
    let listo = false;
    document.addEventListener('DOMContentLoaded', () => {
        const v = $('#cxVista'); if (!v) return;
        v.querySelectorAll('[data-v]').forEach(b => b.addEventListener('click', () => {
            v.querySelectorAll('[data-v]').forEach(x => x.classList.toggle('on', x === b));
            const mundo = b.dataset.v === 'mundo';
            $('#cxPropios').hidden = mundo; $('#cxMundo').hidden = !mundo;
            if (mundo && !listo) { listo = true; iniciar(); }
            history.replaceState(null, '', mundo ? '#mundo' : location.pathname + location.search);
        }));
        if (location.hash === '#mundo') v.querySelector('[data-v="mundo"]').click();
    });

    return { abrir, recargar: () => { cargarElegidos(); } };
})();
