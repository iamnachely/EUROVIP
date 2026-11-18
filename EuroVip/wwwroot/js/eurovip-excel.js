// Cargar Excel (muchos registros de una vez) en Hoteles, Excursiones y Traslados.
//   EVExcel.abrir({ titulo: 'Cargar excursiones con Excel', plantilla: '/Excursiones/Plantilla', url: '/Excursiones/CargarExcel',
//                   uno: 'excursión', varios: 'excursiones', alTerminar: () => recargar() });
// Pedido de la usuaria: «la carga de Excel nunca puede fallar: si no reconoces algo, busca coincidencias y deja elegir con
// Tom Select; editar cada dato, ver cada dato; guiar al usuario a poner datos correctos».
//   1) Descargas la plantilla (sus listas desplegables traen lo que hay en la base EN ESE MOMENTO), la llenas y la subes.
//   2) El servidor la REVISA (no guarda nada): lo que reconoce por parecido lo cambia solo (en amarillo, para revisarlo) y lo que
//      no reconoce lo deja como «por elegir» con las coincidencias.
//   3) Aquí se ve y se edita CADA dato de cada fila (las listas son Tom Select con buscador). «Revisar de nuevo» manda los cambios
//      sin volver a subir el Excel. «No cargar» quita una fila. «Descargar Excel corregido» baja el Excel con todo arreglado.
//   4) «Guardar» crea solo las filas correctas. Las que se guardaron ya no se vuelven a guardar; las demás se pueden seguir corrigiendo.
// No depende de eurovip-ui.js (la pantalla de Hoteles no lo carga): usa el token antiforgery de la página.
window.EVExcel = (() => {
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const clave = s => String(s ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]/g, '');

    let css = false;
    function estilos() {
        if (css) return; css = true;
        const s = document.createElement('style');
        s.textContent = `
.evx-fondo{position:fixed;inset:0;background:rgba(11,18,21,.55);z-index:3000;display:flex;align-items:flex-start;justify-content:center;padding:3vh 16px;overflow:auto}
.evx{background:var(--ev-surface,#fff);color:var(--ev-text,#0B1215);border-radius:24px;box-shadow:0 30px 80px -30px rgba(11,18,21,.45);width:min(1240px,100%);padding:24px 26px;font-family:inherit}
.evx h2{margin:0;font-size:22px;font-weight:800}
.evx .sub{color:var(--ev-muted,#66807C);font-weight:600;font-size:13.5px;margin:4px 0 16px}
.evx-cab{display:flex;gap:12px;align-items:flex-start}
.evx-cab .x{margin-left:auto;border:0;background:var(--ev-surface-2,#F5FBFA);border-radius:12px;width:38px;height:38px;cursor:pointer;font-size:18px;color:inherit}
.evx-pasos{display:flex;gap:10px;flex-wrap:wrap;margin-bottom:14px}
.evx-paso{flex:1 1 220px;border:1px solid var(--ev-line,#E2EFED);border-radius:16px;padding:12px 14px;font-size:13.5px;font-weight:600;color:var(--ev-muted,#66807C)}
.evx-paso b{display:block;color:var(--ev-text,#0B1215);font-size:14.5px;margin-bottom:2px}
.evx-drop{display:flex;gap:14px;align-items:center;border:2px dashed var(--ev-teal,#2BD4C4);border-radius:18px;padding:18px 20px;cursor:pointer;background:var(--ev-surface-2,#F5FBFA);transition:background .2s}
.evx-drop.sobre{background:rgba(116,242,230,.25)}
.evx-drop i{font-size:30px;color:var(--ev-deep,#0E8F86)}
.evx-drop small{display:block;color:var(--ev-muted,#66807C);font-weight:600}
.evx-nota{display:flex;gap:10px;align-items:flex-start;border-radius:14px;padding:12px 14px;margin:14px 0 0;font-weight:600;font-size:14px}
.evx-nota.ok{background:rgba(43,212,196,.14)} .evx-nota.mal{background:rgba(229,72,77,.12)} .evx-nota.info{background:var(--ev-surface-2,#F5FBFA)} .evx-nota.av{background:#FFF7E0}
.evx-tabla{width:100%;border-collapse:collapse;margin-top:12px;font-size:13.5px}
.evx-tabla th{text-align:left;font-size:12px;text-transform:uppercase;letter-spacing:.04em;color:var(--ev-muted,#66807C);padding:8px 10px;border-bottom:1px solid var(--ev-line,#E2EFED);position:sticky;top:0;background:var(--ev-surface,#fff);z-index:1}
.evx-tabla td{padding:9px 10px;border-bottom:1px solid var(--ev-line,#E2EFED);vertical-align:top}
.evx-tabla tr.mal>td{background:rgba(229,72,77,.06)} .evx-tabla tr.off>td{opacity:.45} .evx-tabla tr.hecho>td{background:rgba(43,212,196,.08)}
.evx-tabla .est{font-weight:700} .evx-tabla tr.mal .est{color:#C9373C} .evx-tabla tr:not(.mal) .est{color:var(--ev-deep,#0E8F86)}
.evx-tabla .av{display:block;margin-top:4px;color:#8A6100;font-weight:600;font-size:12.5px}
.evx-lista{max-height:58vh;overflow:auto;border-radius:12px}
.evx-ed>td{background:var(--ev-surface-2,#F5FBFA)!important;padding:14px 16px!important}
.evx-campos{display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:10px 14px}
.evx-campo label{display:block;font-size:12px;font-weight:800;color:var(--ev-muted,#66807C);margin-bottom:4px}
.evx-campo input,.evx-campo select,.evx-campo textarea{width:100%;border:1px solid var(--ev-line,#E2EFED);border-radius:10px;padding:8px 10px;font:inherit;font-size:13.5px;background:var(--ev-surface,#fff);color:inherit}
.evx-campo textarea{min-height:60px}
.evx-campo.error input,.evx-campo.error select,.evx-campo.error textarea,.evx-campo.error .ts-control{border-color:#E5484D!important;box-shadow:0 0 0 3px rgba(229,72,77,.15)}
.evx-campo.duda .ts-control,.evx-campo.duda select{border-color:#F5A623!important;box-shadow:0 0 0 3px rgba(245,166,35,.18)}
.evx-campo .pista{display:block;font-size:12px;font-weight:600;margin-top:4px;color:#8A6100}
.evx-campo.error .pista{color:#C9373C}
.evx-campo.cambiado label::after{content:' · cambiado';color:var(--ev-deep,#0E8F86)}
.evx-pie{display:flex;gap:10px;justify-content:flex-end;flex-wrap:wrap;margin-top:18px;align-items:center}
.evx-btn{display:inline-flex;align-items:center;gap:8px;border-radius:999px;padding:10px 18px;font-weight:700;font-size:14px;cursor:pointer;border:1px solid var(--ev-line,#E2EFED);background:var(--ev-surface,#fff);color:inherit;text-decoration:none}
.evx-btn.sm{padding:5px 12px;font-size:12.5px}
.evx-btn.pri{background:var(--ev-ink,#0B1215);color:#fff;border-color:transparent}
.evx-btn.ama{background:#F5A623;color:#0B1215;border-color:transparent}
.evx-btn[disabled]{opacity:.45;cursor:not-allowed}
.evx-filtro{display:flex;gap:6px;margin-top:12px;flex-wrap:wrap}
.evx-filtro button{border:1px solid var(--ev-line,#E2EFED);background:none;border-radius:999px;padding:5px 12px;font-weight:700;font-size:12.5px;cursor:pointer;color:inherit}
.evx-filtro button.on{background:var(--ev-ink,#0B1215);color:#fff}
.evx-spin{width:18px;height:18px;border:3px solid var(--ev-line,#E2EFED);border-top-color:var(--ev-deep,#0E8F86);border-radius:50%;animation:evxg 1s linear infinite;flex:none}
@keyframes evxg{to{transform:rotate(360deg)}}
.evx-paso{display:flex;gap:12px;align-items:flex-start;background:var(--ev-surface,#fff)}
.evx-paso .n{flex:none;width:30px;height:30px;border-radius:50%;display:grid;place-items:center;font-weight:800;font-size:14px;background:var(--ev-surface-2,#F5FBFA);color:var(--ev-deep,#0E8F86);border:2px solid var(--ev-line,#E2EFED)}
.evx-paso.on{border-color:var(--ev-teal,#2BD4C4);box-shadow:0 0 0 3px rgba(43,212,196,.15)} .evx-paso.on .n{background:var(--ev-teal,#2BD4C4);color:#0B1215;border-color:transparent}
.evx-paso.ya .n{background:var(--ev-deep,#0E8F86);color:#fff;border-color:transparent}
.evx-res{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:10px;margin-top:14px}
.evx-res button{all:unset;cursor:pointer;border:1px solid var(--ev-line,#E2EFED);border-radius:16px;padding:12px 14px;display:flex;flex-direction:column;gap:2px;transition:transform .15s,box-shadow .15s;background:var(--ev-surface,#fff)}
.evx-res button:hover{transform:translateY(-1px);box-shadow:0 8px 20px -14px rgba(11,18,21,.5)}
.evx-res button.on{outline:2px solid var(--ev-ink,#0B1215);outline-offset:-2px}
.evx-res b{font-size:24px;font-weight:800;line-height:1.1} .evx-res span{font-size:12.5px;font-weight:700;color:var(--ev-muted,#66807C)}
.evx-res .ok b{color:var(--ev-deep,#0E8F86)} .evx-res .mal b{color:#C9373C} .evx-res .av b{color:#B07A00}
.evx-grupo{margin-top:12px} .evx-grupo:first-child{margin-top:0}
.evx-grupo>h4{margin:0 0 8px;font-size:12.5px;font-weight:800;text-transform:uppercase;letter-spacing:.05em;display:flex;align-items:center;gap:8px;color:var(--ev-text,#0B1215)}
.evx-grupo>h4 small{font-weight:700;text-transform:none;letter-spacing:0;color:var(--ev-muted,#66807C)}
.evx-grupo.info .evx-campos{grid-template-columns:1fr}
.evx-grupo.info .evx-campo{display:grid;grid-template-columns:150px 1fr;gap:10px;align-items:start}
.evx-grupo.info .evx-campo label{margin-top:9px}
.evx-campo .req{color:#C9373C;margin-left:2px}
.evx-errs{margin:6px 0 0;padding:0;list-style:none;display:flex;flex-direction:column;gap:3px}
.evx-errs li{font-weight:600;font-size:12.5px;color:#9F2A2E;display:flex;gap:6px}
.evx-errs li b{color:#C9373C}
.evx-conf{margin-top:14px;border-radius:16px;padding:14px 16px;background:var(--ev-surface-2,#F5FBFA);border:1px solid var(--ev-line,#E2EFED);display:flex;gap:12px;align-items:center;flex-wrap:wrap;font-weight:600}
@media (max-width:640px){.evx{padding:18px 16px;border-radius:18px}.evx-grupo.info .evx-campo{grid-template-columns:1fr}.evx-grupo.info .evx-campo label{margin-top:0}.evx-tabla th:nth-child(3),.evx-tabla td:nth-child(3){display:none}}`;
        document.head.appendChild(s);
    }

    async function enviar(url, archivo, extra) {
        const fd = new FormData();
        fd.append('archivo', archivo);
        Object.entries(extra).forEach(([k, v]) => fd.append(k, typeof v === 'string' ? v : JSON.stringify(v)));
        const r = await fetch(url, { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token() } });
        if (extra.descargar === 'true' && r.ok) return r.blob();
        let d = null; try { d = await r.json(); } catch { }
        if (!r.ok) throw new Error(d?.mensaje || 'No se pudo leer el Excel. Intenta de nuevo.');
        return d;
    }

    function abrir(o) {
        estilos();
        const fondo = document.createElement('div');
        fondo.className = 'evx-fondo';
        fondo.innerHTML = `<div class="evx" role="dialog" aria-modal="true" aria-label="${esc(o.titulo)}">
            <div class="evx-cab"><div><h2><i class="bi bi-file-earmark-excel"></i> ${esc(o.titulo)}</h2>
                <div class="sub">Carga muchos ${esc(o.varios)} de una vez. Lo que no se reconozca te lo muestro con las coincidencias para elegir: corriges aquí mismo, sin volver a subir el Excel.</div></div>
                <button type="button" class="x" data-evx="cerrar" title="Cerrar"><i class="bi bi-x-lg"></i></button></div>
            <div class="evx-pasos">
                <div class="evx-paso on" data-paso="1"><span class="n">1</span><div><b>Descarga la plantilla</b>Las columnas con flecha traen las listas de Eurovip de hoy (destinos, proveedores, monedas…). Las de la hoja «Instrucciones» explican cada columna.</div></div>
                <div class="evx-paso" data-paso="2"><span class="n">2</span><div><b>Llénala y súbela</b>Una fila por ${esc(o.uno)}. No te preocupes por escribir exacto: «punta kana» o «RD$ 1,250» se entienden.</div></div>
                <div class="evx-paso" data-paso="3"><span class="n">3</span><div><b>Revisa, corrige y guarda</b>Te digo TODO lo que falta en cada fila. Corriges aquí mismo y guardas: todavía no se guarda nada.</div></div>
            </div>
            <div style="display:flex;gap:10px;flex-wrap:wrap;margin-bottom:12px"><a class="evx-btn" href="${esc(o.plantilla)}"><i class="bi bi-download"></i> Descargar plantilla</a></div>
            <label class="evx-drop" data-evx="drop"><i class="bi bi-cloud-arrow-up"></i>
                <div><b style="font-size:15.5px">Arrastra aquí el Excel o haz clic para elegirlo</b><small>Archivo .xlsx con la misma forma que la plantilla</small></div>
                <input type="file" accept=".xlsx,.xlsm" hidden data-evx="archivo"></label>
            <div data-evx="resultado"></div>
            <div class="evx-pie">
                <button type="button" class="evx-btn" data-evx="cerrar">Cerrar</button>
                <button type="button" class="evx-btn" data-evx="bajar" hidden><i class="bi bi-file-earmark-arrow-down"></i> Descargar Excel corregido</button>
                <button type="button" class="evx-btn ama" data-evx="revisar" hidden><i class="bi bi-arrow-repeat"></i> Revisar de nuevo</button>
                <button type="button" class="evx-btn pri" data-evx="guardar" disabled><i class="bi bi-cloud-check"></i> Guardar</button></div>
        </div>`;
        document.body.appendChild(fondo);
        const $ = s => fondo.querySelector(`[data-evx="${s}"]`);
        // cambios: { "fila": { "Columna": "valor" } } · omitir: filas que no se cargan · hechas: filas ya guardadas (no se repiten)
        let archivo = null, guardadoAlguno = false, ultimo = null, filtro = 'todas', cambios = {}, omitir = new Set(), hechas = new Map(), abiertas = new Set(), pendientes = false, mapa = {};   // mapa: columna del Excel → columna de la plantilla (la elige la usuaria)
        let opcionesHtml = {}, cols = null;   // cols: columnas con sus listas (el servidor las manda solo la primera vez: hay miles de destinos)

        const cerrar = () => { fondo.remove(); document.removeEventListener('keydown', tecla); if (guardadoAlguno) o.alTerminar?.(); };
        const tecla = e => { if (e.key === 'Escape' && !e.target.closest?.('.ts-wrapper')) cerrar(); };
        document.addEventListener('keydown', tecla);
        fondo.addEventListener('click', e => { if (e.target === fondo || e.target.closest('[data-evx="cerrar"]')) cerrar(); });

        const paso = n => fondo.querySelectorAll('[data-paso]').forEach(p => { const k = +p.dataset.paso; p.classList.toggle('on', k === n); p.classList.toggle('ya', k < n); });
        const extra = guardar => ({ guardar: guardar ? 'true' : 'false', sinListas: cols ? 'true' : 'false', cambios, mapa, omitir: [...new Set([...omitir, ...hechas.keys()])] });

        /** Opciones de una columna como HTML (se arma una vez: hay listas de miles). */
        function todas(col) {
            if (opcionesHtml[col.nombre] == null)
                opcionesHtml[col.nombre] = col.opciones.map(x => `<option value="${esc(x.valor)}">${esc(x.valor)}${x.texto && clave(x.texto) !== clave(x.valor) ? ' · ' + esc(x.texto) : ''}${x.extra && clave(x.extra) !== clave(x.valor) ? ' (' + esc(x.extra) + ')' : ''}</option>`).join('');
            return opcionesHtml[col.nombre];
        }

        function campo(f, col) {
            const errs = (f.errores || []).filter(e => e.columna === col.nombre);
            const v = f.valores[col.nombre] ?? '', duda = f.dudas.find(d => d.columna === col.nombre), err = !f.ok && (errs.length > 0 || f.columna === col.nombre);
            const cambiado = cambios[f.fila]?.[col.nombre] !== undefined;
            const cls = `evx-campo${err ? ' error' : ''}${duda ? ' duda' : ''}${cambiado ? ' cambiado' : ''}`;
            const id = `evx-${f.fila}-${clave(col.nombre)}`;
            let control, pista = '';
            if (col.opciones?.length) {
                const sug = duda?.sugerencias || [];
                const actual = v && !col.opciones.some(x => x.valor === v) ? `<option value="${esc(v)}">«${esc(v)}» (no está en la lista)</option>` : '';
                const elegida = sug.length === 1 && !cambiado ? sug[0].valor : v;
                control = `<select id="${id}" data-fila="${f.fila}" data-col="${esc(col.nombre)}">
                    <option value="">— ${col.opcional === false ? 'Elige' : 'Vacío'} —</option>${actual}
                    ${sug.length ? `<optgroup label="Coincidencias con «${esc(duda.escrito)}»">${sug.map(x => `<option value="${esc(x.valor)}">${esc(x.valor)}${x.texto && clave(x.texto) !== clave(x.valor) ? ' · ' + esc(x.texto) : ''}${x.extra ? ' (' + esc(x.extra) + ')' : ''}</option>`).join('')}</optgroup>` : ''}
                    <optgroup label="Todas">${todas(col)}</optgroup></select>`;
                // el valor elegido se marca después (las opciones pueden repetirse entre «Coincidencias» y «Todas»)
                control = control.replace(`<option value="${esc(elegida)}">`, `<option value="${esc(elegida)}" selected>`);
                if (duda) pista = sug.length === 0 ? `No encontré «${esc(duda.escrito)}»: escribe en la lista para buscarlo.`
                    : sug.length === 1 ? `¿«${esc(duda.escrito)}» es este? Lo dejé elegido: cámbialo si no es.`
                    : `«${esc(duda.escrito)}» se parece a ${sug.length}: elige el correcto.`;
                if (sug.length === 1 && !cambiado) (cambios[f.fila] ||= {})[col.nombre] = sug[0].valor;
            } else if (col.tipo === 'largo') control = `<textarea id="${id}" data-fila="${f.fila}" data-col="${esc(col.nombre)}">${esc(v)}</textarea>`;
            else control = `<input id="${id}" data-fila="${f.fila}" data-col="${esc(col.nombre)}" value="${esc(v)}" ${col.tipo === 'numero' ? 'inputmode="decimal"' : ''}>`;
            if (err && !duda) pista = esc(errs.map(e => e.mensaje).join(' ') || f.mensaje || '');
            return `<div class="${cls}"><label for="${id}">${esc(col.nombre)}${col.obligatoria ? '<span class="req" title="Obligatorio">*</span>' : ''}</label><div>${control}${pista ? `<span class="pista">${pista}</span>` : ''}</div></div>`;
        }

        const INFO = ['incluye', 'noincluye', 'condiciones', 'documentacion', 'importante', 'ofertaespecial'];
        /** Los datos de la fila en 3 secciones, para no abrumar: primero lo obligatorio, luego lo opcional y aparte la info para el cliente. */
        function grupos(f) {
            const cs = ultimo.columnas, info = cs.filter(c => INFO.includes(clave(c.nombre)));
            const obl = cs.filter(c => c.obligatoria && !info.includes(c)), opc = cs.filter(c => !c.obligatoria && !info.includes(c));
            const g = (titulo, sub, lista, cls = '') => lista.length ? `<div class="evx-grupo ${cls}"><h4>${titulo} <small>${sub}</small></h4><div class="evx-campos">${lista.map(c => campo(f, c)).join('')}</div></div>` : '';
            return g('<i class="bi bi-asterisk" style="color:#C9373C"></i> Obligatorio', 'sin esto no se puede guardar', obl)
                + g('<i class="bi bi-sliders"></i> Opcional', 'si lo dejas vacío se usa el valor por defecto', opc)
                + g('<i class="bi bi-info-circle"></i> Información para el cliente', 'opcional · se ve en la web · una cosa por línea', info, 'info');
        }

        function editor(f) {
            return `<tr class="evx-ed" data-ed="${f.fila}"><td colspan="5">
                ${grupos(f)}
                <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:12px;flex-wrap:wrap">
                    <span style="color:var(--ev-muted,#66807C);font-weight:600;font-size:12.5px;align-self:center">Los cambios se aplican al pulsar «Revisar de nuevo».</span>
                    <button type="button" class="evx-btn sm ama" data-revisar><i class="bi bi-arrow-repeat"></i> Revisar de nuevo</button></div></td></tr>`;
        }

        /** «Columnas de tu Excel»: si el Excel no es la plantilla o trae columnas con otro nombre, se pregunta qué es cada una. Nunca falla. */
        function panelPlantilla(r) {
            const p = r.plantilla;
            if (!p || (!p.sinReconocer?.length && !p.faltan?.length)) return '';
            const nombres = (cols || r.columnas).map(c => c.nombre);
            const filas = p.sinReconocer.map(s => {
                if (!s.asignada && mapa[s.clave] === undefined && s.sugerencias.length === 1) { mapa[s.clave] = s.sugerencias[0]; pendientes = true; }   // se propone y se aplica al revisar
                const elegida = mapa[s.clave] ?? s.asignada ?? (s.sugerencias.length === 1 ? s.sugerencias[0] : null);
                const sola = s.asignada && mapa[s.clave] === undefined;
                return `<tr><td><b>«${esc(s.titulo)}»</b>${s.ejemplo ? `<span class="av" style="color:var(--ev-muted,#66807C)">Ej.: ${esc(String(s.ejemplo).slice(0, 60))}</span>` : ''}</td>
                    <td><select data-mapa="${esc(s.clave)}"><option value="">— No usar esta columna —</option>
                        ${s.sugerencias.length ? `<optgroup label="Se parece a">${s.sugerencias.map(n => `<option value="${esc(n)}">${esc(n)}</option>`).join('')}</optgroup>` : ''}
                        <optgroup label="Columnas de la plantilla">${nombres.map(n => `<option value="${esc(n)}">${esc(n)}</option>`).join('')}</optgroup></select>
                        ${sola ? '<span class="av"><i class="bi bi-magic"></i> La reconocí sola: cámbiala si no es.</span>' : !elegida ? '<span class="av">Elige qué dato es, o «No usar».</span>' : ''}</td></tr>`
                    .replace(`<option value="${esc(elegida ?? '')}">`, `<option value="${esc(elegida ?? '')}" selected>`);
            }).join('');
            return `<div class="evx-nota ${p.distinta ? 'mal' : 'av'}" style="display:block"><div style="display:flex;gap:10px"><i class="bi ${p.distinta ? 'bi-file-earmark-x' : 'bi-columns-gap'}"></i><div>${esc(p.mensaje || 'Revisa las columnas de tu Excel.')}</div></div>
                ${filas ? `<table class="evx-tabla" style="margin-top:10px;background:var(--ev-surface,#fff);border-radius:12px"><thead><tr><th>Columna de tu Excel</th><th style="width:55%">¿Qué dato es?</th></tr></thead><tbody>${filas}</tbody></table>` : ''}
                ${p.faltan?.length ? `<div style="margin-top:10px">Faltan: ${p.faltan.map(n => `<span class="evx-btn sm" style="cursor:default;margin:2px">${esc(n)}</span>`).join('')}</div>` : ''}
                <div style="margin-top:10px;display:flex;gap:8px;flex-wrap:wrap"><button type="button" class="evx-btn sm ama" data-revisar><i class="bi bi-arrow-repeat"></i> Aplicar columnas y revisar de nuevo</button>
                    <a class="evx-btn sm" href="${esc(o.plantilla)}"><i class="bi bi-download"></i> Descargar la plantilla correcta</a></div></div>`;
        }

        function pintar(r) {
            if (!cols || r.columnas.some(c => c.opciones?.length)) { cols = r.columnas; opcionesHtml = {}; }
            r.columnas = cols;
            ultimo = r;
            const visibles = r.filas.filter(f => !hechas.has(f.fila) || f.id);
            const porCorregir = visibles.filter(f => !f.ok && !omitir.has(f.fila)).length;
            const filas = visibles.filter(f => filtro === 'todas' || (filtro === 'mal' ? !f.ok && !omitir.has(f.fila) : filtro === 'av' ? f.avisos.length || f.dudas.length : f.ok));
            const clase = r.guardado ? (r.conError ? 'info' : 'ok') : r.conError ? (r.correctas ? 'info' : 'mal') : 'ok';
            const nAv = visibles.filter(f => f.avisos.length || f.dudas.length).length;
            $('resultado').innerHTML = panelPlantilla(r) + `<div class="evx-nota ${clase}"><i class="bi ${clase === 'mal' ? 'bi-x-circle-fill' : clase === 'ok' ? 'bi-check-circle-fill' : 'bi-info-circle-fill'}"></i><div>${esc(r.mensaje)}</div></div>
                ${pendientes ? '<div class="evx-nota av"><i class="bi bi-pencil-square"></i><div>Tienes cambios sin revisar: pulsa <b>«Revisar de nuevo»</b> para comprobarlos.</div></div>' : ''}
                <div class="evx-res">
                    <button type="button" data-f="todas" class="${filtro === 'todas' ? 'on' : ''}"><b>${visibles.length}</b><span><i class="bi bi-list-ul"></i> Filas del Excel</span></button>
                    <button type="button" data-f="ok" class="ok ${filtro === 'ok' ? 'on' : ''}"><b>${r.correctas}</b><span><i class="bi bi-check2-circle"></i> Listas para guardar</span></button>
                    <button type="button" data-f="mal" class="mal ${filtro === 'mal' ? 'on' : ''}"><b>${porCorregir}</b><span><i class="bi bi-exclamation-triangle"></i> Por corregir</span></button>
                    <button type="button" data-f="av" class="av ${filtro === 'av' ? 'on' : ''}"><b>${nAv}</b><span><i class="bi bi-magic"></i> Reconocidas por parecido</span></button></div>
                ${porCorregir ? `<div style="display:flex;justify-content:flex-end;margin-top:10px"><button type="button" class="evx-btn sm ama" data-abrir-todas><i class="bi bi-pencil"></i> Abrir todas las filas por corregir</button></div>` : ''}
                <div class="evx-lista"><table class="evx-tabla"><thead><tr><th style="width:56px">Fila</th><th>${esc(o.uno[0].toUpperCase() + o.uno.slice(1))}</th><th>Datos</th><th style="width:34%">Estado</th><th style="width:170px"></th></tr></thead><tbody>
                ${filas.map(f => {
                    const off = omitir.has(f.fila), hecho = hechas.has(f.fila);
                    return `<tr class="${hecho ? 'hecho' : off ? 'off' : f.ok ? '' : 'mal'}"><td>${f.fila}</td><td><b>${esc(f.nombre || '—')}</b></td><td>${esc(f.detalle || '')}
                        ${f.avisos.map(a => `<span class="av"><i class="bi bi-magic"></i> ${esc(a)}</span>`).join('')}</td>
                    <td class="est">${hecho ? `<i class="bi bi-check2-all"></i> Guardado n.º ${hechas.get(f.fila)}` : off ? '<i class="bi bi-slash-circle"></i> No se carga'
                        : f.ok ? '<i class="bi bi-check2"></i> Correcta'
                        : (f.errores?.length > 1
                            ? `<i class="bi bi-exclamation-triangle"></i> ${f.errores.length} cosas por corregir<ul class="evx-errs">${f.errores.map(e => `<li>${e.columna ? `<b>${esc(e.columna)}:</b>` : ''} ${esc(e.mensaje)}</li>`).join('')}</ul>`
                            : `<i class="bi bi-exclamation-triangle"></i> <span style="font-weight:600">${esc(f.errores?.[0]?.mensaje || f.mensaje)}</span>`)}</td>
                    <td style="text-align:right;white-space:nowrap">${hecho ? '' : `<button type="button" class="evx-btn sm${!f.ok && !off ? ' ama' : ''}" data-editar="${f.fila}"><i class="bi bi-${abiertas.has(f.fila) ? 'chevron-up' : f.ok ? 'eye' : 'pencil'}"></i> ${abiertas.has(f.fila) ? 'Cerrar' : f.ok ? 'Ver' : 'Corregir'}</button>
                        <button type="button" class="evx-btn sm" data-omitir="${f.fila}" title="${off ? 'Volver a cargar esta fila' : 'No cargar esta fila'}"><i class="bi bi-${off ? 'arrow-counterclockwise' : 'slash-circle'}"></i></button>`}</td></tr>
                    ${abiertas.has(f.fila) && !hecho ? editor(f) : ''}`;
                }).join('') || '<tr><td colspan="5" style="text-align:center;color:var(--ev-muted)">No hay filas con ese filtro.</td></tr>'}
                </tbody></table></div>`;
            fondo.querySelectorAll('.evx-res [data-f]').forEach(b => b.onclick = () => { filtro = b.dataset.f; pintar(ultimo); });
            fondo.querySelector('[data-abrir-todas]')?.addEventListener('click', () => { r.filas.filter(f => !f.ok && !omitir.has(f.fila)).slice(0, 40).forEach(f => abiertas.add(f.fila)); filtro = 'mal'; pintar(ultimo); });
            fondo.querySelectorAll('[data-editar]').forEach(b => b.onclick = () => { const n = +b.dataset.editar; abiertas.has(n) ? abiertas.delete(n) : abiertas.add(n); pintar(ultimo); });
            fondo.querySelectorAll('[data-omitir]').forEach(b => b.onclick = () => { const n = +b.dataset.omitir; omitir.has(n) ? omitir.delete(n) : omitir.add(n); pendientes = true; pintar(ultimo); });
            fondo.querySelectorAll('[data-revisar]').forEach(b => b.onclick = revisar);
            fondo.querySelectorAll('[data-mapa]').forEach(s => s.addEventListener('change', () => {
                mapa[s.dataset.mapa] = s.value;   // "" = no usar
                pendientes = true; const g = $('guardar'); g.disabled = true; $('revisar').classList.add('ama');
            }));
            fondo.querySelectorAll('.evx-ed [data-col]').forEach(el => el.addEventListener('change', () => cambiar(el)));
            const g = $('guardar'), quedan = r.filas.filter(f => f.ok && !hechas.has(f.fila) && !omitir.has(f.fila)).length;
            g.disabled = !quedan || pendientes;
            g.title = pendientes ? 'Primero pulsa «Revisar de nuevo» para comprobar tus cambios.' : '';
            g.innerHTML = `<i class="bi bi-cloud-check"></i> Guardar ${quedan} ${quedan === 1 ? o.uno : o.varios}`;
            $('revisar').hidden = false; $('bajar').hidden = false;
            paso(3);
            $('revisar').classList.toggle('ama', pendientes);
        }

        /** Un dato cambiado en el editor. Si era una duda, se aplica igual a las demás filas con lo mismo escrito en esa columna. */
        function cambiar(el) {
            const n = +el.dataset.fila, col = el.dataset.col, v = el.value;
            (cambios[n] ||= {})[col] = v;
            const f = ultimo.filas.find(x => x.fila === n), duda = f?.dudas.find(d => d.columna === col);
            let otras = 0;
            if (duda && v) ultimo.filas.forEach(x => {
                if (x.fila === n || hechas.has(x.fila)) return;
                if (x.dudas.some(d => d.columna === col && clave(d.escrito) === clave(duda.escrito)) && cambios[x.fila]?.[col] !== v) { (cambios[x.fila] ||= {})[col] = v; otras++; }
            });
            if (otras) aviso(`También puse «${v}» en ${otras} fila(s) más que decían «${duda.escrito}».`);
            pendientes = true;
            el.closest('.evx-campo')?.classList.add('cambiado');
            const g = $('guardar'); g.disabled = true; g.title = 'Primero pulsa «Revisar de nuevo» para comprobar tus cambios.';
            $('revisar').classList.add('ama');
        }

        /** Pregunta dentro de la ventana, junto a los botones (se ve mejor que el cuadro del navegador). */
        function confirmar(texto) {
            return new Promise(ok => {
                fondo.querySelector('.evx-conf')?.remove();
                const d = document.createElement('div'); d.className = 'evx-conf';
                d.innerHTML = `<i class="bi bi-cloud-check" style="font-size:22px;color:var(--ev-deep,#0E8F86)"></i><div style="flex:1 1 260px">${esc(texto)}</div>
                    <button type="button" class="evx-btn sm" data-no>Todavía no</button><button type="button" class="evx-btn sm pri" data-si><i class="bi bi-check2"></i> Sí, guardar</button>`;
                fondo.querySelector('.evx-pie').before(d);
                d.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
                d.querySelector('[data-si]').onclick = () => { d.remove(); ok(true); };
                d.querySelector('[data-no]').onclick = () => { d.remove(); ok(false); };
            });
        }

        function aviso(t) {
            const d = document.createElement('div'); d.className = 'evx-nota av'; d.innerHTML = `<i class="bi bi-magic"></i><div>${esc(t)}</div>`;
            $('resultado').prepend(d); setTimeout(() => d.remove(), 6000);
        }

        async function revisar() {
            if (!archivo) return;
            const b = $('revisar'), html = b.innerHTML; b.disabled = true; b.innerHTML = '<span class="evx-spin"></span> Revisando…';
            try { const r = await enviar(o.url, archivo, extra(false)); pendientes = false; cambiosAplicados(r); pintar(r); }
            catch (e) { aviso(e.message); }
            finally { b.disabled = false; b.innerHTML = html; }
        }

        /** Tras revisar: las filas que quedaron bien se cierran; las que siguen mal se quedan abiertas. Las ya guardadas siguen marcadas. */
        function cambiosAplicados(r) {
            r.filas.forEach(f => { if (f.ok) abiertas.delete(f.fila); });
            // filas guardadas antes: el servidor no las devuelve (van en «omitir»); se agregan para que se vean
            hechas.forEach((id, fila) => { if (!r.filas.some(f => f.fila === fila)) r.filas.push({ fila, nombre: '', detalle: '', ok: true, id, avisos: [], dudas: [], valores: {} }); });
            r.filas.sort((a, b) => a.fila - b.fila);
        }

        async function leer(f) {
            if (!f) return;
            paso(2); archivo = f; filtro = 'todas'; cambios = {}; omitir = new Set(); hechas = new Map(); abiertas = new Set(); pendientes = false; mapa = {};
            $('guardar').disabled = true;
            $('resultado').innerHTML = `<div class="evx-nota info"><span class="evx-spin"></span><div>Revisando <b>${esc(f.name)}</b> y buscando coincidencias… (todavía no se guarda nada)</div></div>`;
            try {
                const r = await enviar(o.url, f, extra(false));
                // Se abren solas las filas que tienen algo por elegir (hasta 15)
                r.filas.filter(x => !x.ok).slice(0, 15).forEach(x => abiertas.add(x.fila));
                pintar(r);
            }
            catch (e) { $('resultado').innerHTML = `<div class="evx-nota mal"><i class="bi bi-x-circle-fill"></i><div>${esc(e.message)}</div></div>`; }
            finally { $('archivo').value = ''; }
        }
        $('archivo').addEventListener('change', e => leer(e.target.files[0]));
        const drop = $('drop');
        ['dragenter', 'dragover'].forEach(t => drop.addEventListener(t, e => { e.preventDefault(); drop.classList.add('sobre'); }));
        ['dragleave', 'drop'].forEach(t => drop.addEventListener(t, e => { e.preventDefault(); drop.classList.remove('sobre'); }));
        drop.addEventListener('drop', e => leer(e.dataTransfer.files[0]));
        $('revisar').addEventListener('click', revisar);

        $('bajar').addEventListener('click', async () => {
            if (!archivo) return;
            const b = $('bajar'), html = b.innerHTML; b.disabled = true; b.innerHTML = '<span class="evx-spin"></span> Preparando…';
            try {
                const blob = await enviar(o.url, archivo, { ...extra(false), descargar: 'true' });
                const a = document.createElement('a'); a.href = URL.createObjectURL(blob);
                a.download = archivo.name.replace(/\.xls[xm]$/i, '') + ' (corregido).xlsx'; document.body.appendChild(a); a.click(); a.remove();
                setTimeout(() => URL.revokeObjectURL(a.href), 5000);
            } catch (e) { aviso(e.message); }
            finally { b.disabled = false; b.innerHTML = html; }
        });

        $('guardar').addEventListener('click', async () => {
            if (!archivo || !ultimo || pendientes) return;
            const n = ultimo.filas.filter(f => f.ok && !hechas.has(f.fila) && !omitir.has(f.fila)).length;
            if (!n) return;
            const malas = ultimo.filas.filter(f => !f.ok && !omitir.has(f.fila)).length;
            if (!await confirmar(`¿Guardar ${n} ${n === 1 ? o.uno : o.varios}?` + (malas ? ` Las ${malas} fila(s) por corregir NO se guardan: puedes corregirlas después y volver a guardar.` : ''))) return;
            const g = $('guardar');
            g.disabled = true; g.innerHTML = '<span class="evx-spin"></span> Guardando…';
            try {
                const r = await enviar(o.url, archivo, extra(true));
                r.filas.filter(f => f.ok && f.id).forEach(f => hechas.set(f.fila, f.id));
                guardadoAlguno ||= r.guardadas > 0;
                cambiosAplicados(r); filtro = 'todas'; pintar(r);
            }
            catch (e) { aviso(e.message); pintar(ultimo); }
        });
    }
    return { abrir };
})();
