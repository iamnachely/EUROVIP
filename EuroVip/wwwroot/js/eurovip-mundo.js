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
    let marcados = new Set(), agregando = false;
    let raiz, destinos = [], elegidos = [], tarjetas = { destinos: [] }, actual = null, pagina = null, cargando = false, filtro = '';

    function estilos() {
        if ($('#cxm-css')) return;
        const s = document.createElement('style'); s.id = 'cxm-css';
        s.textContent = `
.cxm-home{border:1px solid var(--ev-line,#E2EFED);border-radius:20px;padding:16px 18px;margin-bottom:16px;background:var(--ev-surface,#fff)}
.cxm-home h3{margin:0 0 4px;font-size:16px;font-weight:800;display:flex;gap:8px;align-items:center}
.cxm-home .sub{color:var(--ev-muted,#66807C);font-weight:600;font-size:13px;margin:0 0 10px}
.cxm-grupo{margin-top:14px;display:grid;grid-template-columns:240px 1fr;gap:14px;align-items:start;padding-top:14px;border-top:1px solid var(--ev-line,#E2EFED)}
.cxm-grupo:first-of-type{border-top:0;padding-top:0}
@media (max-width:860px){.cxm-grupo{grid-template-columns:1fr}}
.cxm-dcard{border-radius:16px;overflow:hidden;border:1px solid var(--ev-line,#E2EFED);background:#fff;box-shadow:0 10px 24px -20px rgba(11,18,21,.5)}
.cxm-dcard .ft{position:relative;height:120px;background:#DDEFEC}.cxm-dcard .ft img{width:100%;height:100%;object-fit:cover;display:block}
.cxm-dcard .vis{position:absolute;left:8px;top:8px;font-size:11px;font-weight:800;border-radius:999px;padding:3px 9px;background:#FEF7C3;color:#854D0E}
.cxm-dcard .vis.si{background:#DCFCE7;color:#166534}
.cxm-dcard .bd{padding:10px 12px}.cxm-dcard b{display:block;font-size:14.5px;font-weight:800}
.cxm-dcard small{display:block;color:var(--ev-muted,#66807C);font-weight:600;font-size:12px}
.cxm-dcard .pr{color:#C2410C;font-weight:800;font-size:13px;margin-top:4px}
.cxm-dcard.off .ft img{filter:grayscale(.7);opacity:.7}
.cxm-sw{display:flex;align-items:center;gap:8px;margin-top:10px;font-size:12.5px;font-weight:800;cursor:pointer;user-select:none;position:relative}
.cxm-sw input{position:absolute;opacity:0;width:0;height:0}
.cxm-sw span{width:38px;height:22px;border-radius:999px;background:#D0D5DD;position:relative;transition:background .2s;flex:none}
.cxm-sw span::after{content:'';position:absolute;left:3px;top:3px;width:16px;height:16px;border-radius:50%;background:#fff;transition:left .2s;box-shadow:0 1px 3px rgba(0,0,0,.25)}
.cxm-sw input:checked+span{background:#16A34A}.cxm-sw input:checked+span::after{left:19px}
.cxm-sw input:focus-visible+span{outline:2px solid #0E8F86;outline-offset:2px}
.cxm-gt{display:block;font-size:13px;text-transform:uppercase;letter-spacing:.04em;color:var(--ev-muted,#66807C);margin-bottom:6px}
.cxm-lista{list-style:none;margin:0;padding:0;display:flex;flex-direction:column;gap:6px}
.cxm-lista li{display:flex;align-items:center;gap:10px;border:1px solid var(--ev-line,#E2EFED);border-radius:14px;padding:6px 8px;background:var(--ev-surface-2,#F5FBFA)}
.cxm-lista input[type=number]{width:52px;border:1px solid var(--ev-line,#E2EFED);border-radius:10px;padding:6px 4px;text-align:center;font:inherit;font-weight:800;background:#fff}
.cxm-lista img,.cxm-lista .ph{width:52px;height:40px;border-radius:9px;object-fit:cover;flex:none;background:#DDEFEC}
.cxm-lista .tx{min-width:0;flex:1}.cxm-lista .t{font-weight:700;font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.cxm-lista small{color:var(--ev-muted,#66807C);font-weight:600;font-size:12px}.cxm-lista small b{color:#C2410C}
.cxm-lista .fu{font-weight:800;color:#C2410C}.cxm-lista .fu.pr{color:#0E8F86}
.cxm-lista .acc{display:flex;gap:2px}.cxm-lista .acc button{border:0;background:none;cursor:pointer;color:var(--ev-muted,#66807C);padding:4px;font-size:14px;border-radius:8px}
.cxm-lista .acc button:hover:not(:disabled){color:var(--ev-ink,#0B1215);background:#fff}.cxm-lista .acc button:disabled{opacity:.3;cursor:default}
.cxm-propios{display:flex;gap:6px;flex-wrap:wrap;margin-top:8px;align-items:center}
.cxm-propios>small{font-size:11.5px;font-weight:800;text-transform:uppercase;letter-spacing:.04em;color:var(--ev-muted,#66807C)}
.cxm-propio{font-size:12px;font-weight:700;border-radius:999px;padding:4px 10px;background:#E6FBF8;color:#0E8F86}
.cxm-aviso{display:flex;gap:10px;align-items:flex-start;border-radius:14px;padding:10px 12px;background:#FFF7E0;color:#854D0E;font-weight:600;font-size:13px;margin:6px 0 4px}
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
.cxm-selbar{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin:0 0 12px;padding:8px 10px;border:1px solid var(--ev-line,#E2EFED);border-radius:14px;background:var(--ev-surface-2,#F5FBFA);position:sticky;top:8px;z-index:3}
.cxm-selinfo{flex:1;color:var(--ev-muted,#66807C);font-weight:600;font-size:13px}.cxm-selinfo b{color:var(--ev-ink,#0B1215)}
.cxm-chk{position:absolute;left:10px;top:10px;z-index:2;cursor:pointer}
.cxm-chk input{position:absolute;opacity:0;width:0;height:0}
.cxm-chk span{width:26px;height:26px;border-radius:8px;display:grid;place-items:center;background:rgba(255,255,255,.92);border:2px solid #fff;color:transparent;box-shadow:0 2px 6px rgba(0,0,0,.25);font-size:15px}
.cxm-chk input:checked+span{background:#0E8F86;border-color:#0E8F86;color:#fff}
.cxm-chk input:focus-visible+span{outline:2px solid #0E8F86;outline-offset:2px}
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
        try { [elegidos, tarjetas] = await Promise.all([EV.api('/Circuitos/MundoElegidos'), EV.api('/Circuitos/MundoTarjetas')]); }
        catch (e) { elegidos = []; tarjetas = { destinos: [] }; EV.toast(e.message, 'err'); }
        pintarHome();
        const n = document.getElementById('cxNMundo'); if (n) n.textContent = elegidos.length ? `${elegidos.length} en la Home` : '';
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

    // CAMBIO 136: cada destino con su tarjeta (encender/apagar en la web) y la lista completa de sus circuitos
    // (Yourttoo + propios) en el orden en que salen en la web. La prioridad se cambia con el número o con las flechas.
    function pintarHome() {
        const lista = tarjetas.destinos || [];
        const ocultas = lista.filter(d => !d.visible).length;
        $('#cxmHome').innerHTML = `<h3><i class="bi bi-house-heart"></i> En la Home <small style="font-weight:600;color:var(--ev-muted)">${elegidos.length} circuito${elegidos.length === 1 ? '' : 's'} de Yourttoo en ${lista.length} destino${lista.length === 1 ? '' : 's'}</small></h3>
            <p class="sub">Cada destino es una tarjeta en la sección «${esc(tarjetas.seccion || 'CIRCUITOS')}» de la Home. Al abrirla, la web muestra sus circuitos (los de Yourttoo elegidos y los tuyos) en el orden de la <b>prioridad</b>: 1 sale primero. Cambia el número o usa las flechas; se guarda solo.</p>
            ${ocultas ? `<div class="cxm-aviso"><i class="bi bi-eye-slash"></i><div>${ocultas === 1 ? 'Hay 1 destino oculto' : `Hay ${ocultas} destinos ocultos`} en la web. Enciéndelos con el interruptor «Se ve en la web» de cada destino.</div></div>` : ''}
            ${lista.length ? lista.map(d => `<div class="cxm-grupo">
                <div class="cxm-dcard ${d.visible ? '' : 'off'}"><div class="ft">${d.imagen ? `<img src="${esc(d.imagen)}" alt="" loading="lazy" onerror="this.remove()">` : ''}<span class="vis ${d.visible ? 'si' : ''}">${d.visible ? '<i class="bi bi-eye"></i> En la web' : '<i class="bi bi-eye-slash"></i> Oculta'}</span></div>
                    <div class="bd"><b>${esc(d.titulo)}</b><small>${esc(d.circuitos || '')}${d.ciudades ? ' · ' + esc(d.ciudades) : ''}</small><div class="pr">Desde ${dinero(d.desde, d.moneda)}</div>
                    <label class="cxm-sw" title="Encender o apagar esta tarjeta en la Home de la web"><input type="checkbox" data-visible="${d.id}" ${d.visible ? 'checked' : ''}><span></span> Se ve en la web</label></div></div>
                <div><b class="cxm-gt"><i class="bi bi-geo-alt-fill"></i> ${esc(d.destino)} · ${d.lista.length} circuito${d.lista.length === 1 ? '' : 's'} en la web, por prioridad</b>
                <ol class="cxm-lista">${d.lista.map((c, i) => `<li>
                    <input type="number" min="1" max="${d.lista.length}" value="${i + 1}" title="Prioridad: 1 sale primero" data-puesto="${esc(d.pais)}|${i}" aria-label="Prioridad">
                    ${c.imagen ? `<img src="${esc(c.imagen)}" alt="" loading="lazy" onerror="this.replaceWith(Object.assign(document.createElement('span'),{className:'ph'}))">` : '<span class="ph"></span>'}
                    <div class="tx"><div class="t">${esc(c.titulo)}</div><small><span class="fu ${c.fuente === 'PROPIO' ? 'pr' : ''}">${c.fuente === 'PROPIO' ? '<i class="bi bi-signpost-split"></i> Carga manual' : '<i class="bi bi-globe-americas"></i> Yourttoo'}</span>${c.dias ? ' · ' + c.dias + ' días' : ''} · Desde <b>${dinero(c.desde, c.moneda)}</b></small></div>
                    <div class="acc"><button type="button" title="Subir" data-mover="${esc(d.pais)}|${i}|-1" ${i === 0 ? 'disabled' : ''}><i class="bi bi-arrow-up"></i></button>
                        <button type="button" title="Bajar" data-mover="${esc(d.pais)}|${i}|1" ${i === d.lista.length - 1 ? 'disabled' : ''}><i class="bi bi-arrow-down"></i></button>
                        ${c.idElegido ? `<button type="button" title="Quitar de la Home" data-quitar="${c.idElegido}"><i class="bi bi-x-lg"></i></button>` : '<span style="width:22px"></span>'}</div></li>`).join('')}</ol></div></div>`).join('')
              : '<div class="cxm-vacio">Todavía no hay circuitos por el mundo en la Home. Elige un destino y pulsa «Agregar a la Home» en los que quieras.</div>'}`;
        const h = $('#cxmHome');
        h.querySelectorAll('[data-quitar]').forEach(b => b.onclick = () => quitar(+b.dataset.quitar));
        h.querySelectorAll('[data-mover]').forEach(b => b.onclick = () => { const [p, i, d] = b.dataset.mover.split('|'); mover(p, +i, +i + +d); });
        h.querySelectorAll('[data-puesto]').forEach(inp => inp.onchange = () => { const [p, i] = inp.dataset.puesto.split('|'); mover(p, +i, (parseInt(inp.value, 10) || 1) - 1); });
        h.querySelectorAll('[data-visible]').forEach(c => c.onchange = () => visible(+c.dataset.visible, c.checked, c));
    }

    async function abrir(pais, pag = 0) {
        if (cargando) return;
        if (pag === 0 && pais !== actual) marcados = new Set();
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
        const libres = p.programas.filter(x => !ids.has(x.codigo));
        caja.innerHTML = `<div style="display:flex;align-items:baseline;gap:10px;margin:0 0 12px;flex-wrap:wrap"><h3 style="margin:0;font-size:19px;font-weight:800">${esc(p.nombre)}</h3>
                <small style="color:var(--ev-muted);font-weight:600">${p.total} circuito${p.total === 1 ? '' : 's'} · del más económico al más caro · precio de Yourttoo por persona en doble (la web aplica el de la agencia)</small></div>
            ${p.programas.length ? `<div class="cxm-selbar">
                <button type="button" class="ev-btn ghost sm" id="cxmSelTodos"><i class="bi bi-check2-all"></i> ${libres.length && libres.every(x => marcados.has(x.codigo)) && p.actual + 1 >= p.paginas ? 'Quitar selección' : 'Seleccionar todos'}</button>
                <span class="cxm-selinfo">${marcados.size ? `<b>${marcados.size}</b> seleccionado${marcados.size === 1 ? '' : 's'}` : 'Marca los circuitos con la casilla o selecciónalos todos'}</span>
                <button type="button" class="ev-btn sm" id="cxmAgregarSel" ${marcados.size && !agregando ? '' : 'disabled'}><i class="bi bi-plus-lg"></i> Agregar ${marcados.size || ''} a la Home</button>
            </div>` : ''}
            ${p.programas.length ? `<div class="cxm-grid">${p.programas.map(x => { const id = ids.get(x.codigo); return `<article class="cxm-card ${id ? 'si' : ''}">
                <div class="ft">${id ? '' : `<label class="cxm-chk" title="Seleccionar"><input type="checkbox" data-marcar="${esc(x.codigo)}" ${marcados.has(x.codigo) ? 'checked' : ''}><span><i class="bi bi-check-lg"></i></span></label>`}${x.imagen ? `<img src="${esc(x.imagen)}" alt="" loading="lazy" onerror="this.remove()">` : ''}${x.dias ? `<span class="dias">${x.dias} días</span>` : ''}${id ? '<span class="ok"><i class="bi bi-check2"></i> En la Home</span>' : ''}</div>
                <div class="bd"><h4>${esc(x.titulo)}</h4>${x.ciudades ? `<small>${esc(x.ciudades)}</small>` : ''}${x.categoria ? `<small>${esc(x.categoria)}</small>` : ''}
                    <div class="pr">Desde <b>${dinero(x.desde, x.moneda)}</b><br><small>por persona en doble · ${esc(x.codigo)}</small></div></div>
                <button type="button" class="sel" data-codigo="${esc(x.codigo)}" data-id="${id ?? ''}">${id ? '<i class="bi bi-x-lg"></i> Quitar de la Home' : '<i class="bi bi-plus-lg"></i> Agregar a la Home'}</button></article>`; }).join('')}</div>`
              : '<div class="cxm-vacio">Yourttoo no tiene circuitos en este destino.</div>'}
            ${p.actual + 1 < p.paginas ? `<div style="text-align:center;margin-top:14px"><button type="button" class="ev-btn ghost" id="cxmMas"><i class="bi bi-chevron-down"></i> Ver más (${p.programas.length} de ${p.total})</button></div>` : ''}`;
        caja.querySelectorAll('button.sel').forEach(b => b.onclick = () => b.dataset.id ? quitar(+b.dataset.id) : agregar(b.dataset.codigo, b));
        $('#cxmMas')?.addEventListener('click', () => abrir(p.pais, p.actual + 1));
        caja.querySelectorAll('[data-marcar]').forEach(c => c.onchange = () => { c.checked ? marcados.add(c.dataset.marcar) : marcados.delete(c.dataset.marcar); pintarProgramas(); });
        $('#cxmSelTodos')?.addEventListener('click', seleccionarTodos);
        $('#cxmAgregarSel')?.addEventListener('click', agregarMarcados);
    }

    // Pedido de la usuaria: «botones para seleccionar y agregar todos los circuitos de un destino, uno para seleccionar y otro para agregar».
    // «Seleccionar todos» trae primero las páginas que falten del destino, así se marcan TODOS (no solo los que se ven).
    async function seleccionarTodos() {
        const ids = new Set(elegidos.map(x => x.codigo));
        const todos = () => pagina.programas.filter(x => !ids.has(x.codigo)).map(x => x.codigo);
        if (todos().length && todos().every(c => marcados.has(c)) && pagina.actual + 1 >= pagina.paginas) { marcados = new Set(); pintarProgramas(); return; }
        const b = $('#cxmSelTodos'); if (b) { b.disabled = true; b.innerHTML = '<span class="ui-spin"></span> Trayendo todos…'; }
        try {
            while (pagina.actual + 1 < pagina.paginas) {
                const r = await EV.api(`/Circuitos/MundoProgramas?pais=${encodeURIComponent(pagina.pais)}&pagina=${pagina.actual + 1}`);
                pagina = { ...r, programas: [...pagina.programas, ...r.programas] };
                const bb = $('#cxmSelTodos'); if (bb) bb.innerHTML = `<span class="ui-spin"></span> Trayendo ${pagina.programas.length} de ${pagina.total}…`;
            }
        } catch (e) { EV.toast(e.message, 'err'); }
        todos().forEach(c => marcados.add(c));
        pintarProgramas();
    }
    async function agregarMarcados() {
        if (agregando || !marcados.size) return;
        agregando = true;
        const lista = [...marcados]; let ok = 0, fallos = 0;
        const b = $('#cxmAgregarSel');
        for (const [n, codigo] of lista.entries()) {
            if (b) b.innerHTML = `<span class="ui-spin"></span> Agregando ${n + 1} de ${lista.length}…`;
            try { await EV.post('/Circuitos/MundoAgregar', { Codigo: codigo, Pais: actual }); ok++; marcados.delete(codigo); }
            catch (e) { fallos++; }
        }
        agregando = false;
        EV.toast(fallos ? `Agregados ${ok}; ${fallos} no se pudieron agregar (siguen marcados, vuelve a intentarlo).` : `${ok} circuito${ok === 1 ? '' : 's'} agregado${ok === 1 ? '' : 's'} a la Home.`, fallos ? 'err' : 'ok');
        await cargarElegidos(); pintarDestinos(); if (pagina) pintarProgramas();
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
    async function mover(pais, i, j) {
        const d = (tarjetas.destinos || []).find(x => x.pais === pais); if (!d) return;
        const l = d.lista.slice(); j = Math.max(0, Math.min(l.length - 1, j));
        if (j === i) { pintarHome(); return; }
        const [x] = l.splice(i, 1); l.splice(j, 0, x);
        d.lista = l; pintarHome();   // se ve al instante; luego se confirma con el servidor
        try { await EV.post('/Circuitos/MundoPrioridad', { Pais: pais, Orden: l.map(c => ({ Fuente: c.fuente, Codigo: c.codigo })) }); EV.toast('Prioridad guardada.', 'ok'); }
        catch (e) { EV.toast(e.message, 'err'); }
        await cargarElegidos();
    }
    async function visible(id, si, caja) {
        caja.disabled = true;
        try { const r = await EV.post('/Circuitos/MundoVisible', { Id: id, Visible: si }); EV.toast(r.mensaje, 'ok'); }
        catch (e) { EV.toast(e.message, 'err'); }
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
        // cuántos hay ya en la Home (se ve en la pestaña sin abrirla)
        EV.api('/Circuitos/MundoElegidos').then(l => { const n = document.getElementById('cxNMundo'); if (n && l.length) n.textContent = `${l.length} en la Home`; }).catch(() => { });
    });

    return { abrir, recargar: () => { cargarElegidos(); } };
})();
