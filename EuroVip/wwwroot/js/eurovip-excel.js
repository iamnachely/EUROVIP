// Cargar Excel (muchos registros de una vez) en Hoteles, Excursiones y Traslados.
//   EVExcel.abrir({ titulo: 'Cargar excursiones con Excel', plantilla: '/Excursiones/Plantilla', url: '/Excursiones/CargarExcel',
//                   uno: 'excursión', varios: 'excursiones', alTerminar: () => recargar() });
// 1) Descargas la plantilla, la llenas y la subes. 2) El servidor la REVISA fila por fila (no guarda nada) y aquí se ve
// qué filas están bien y qué le falta a cada una. 3) «Guardar» crea solo las correctas, con las mismas reglas del formulario.
// No depende de eurovip-ui.js (la pantalla de Hoteles no lo carga): usa el token antiforgery de la página.
window.EVExcel = (() => {
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    let css = false;
    function estilos() {
        if (css) return; css = true;
        const s = document.createElement('style');
        s.textContent = `
.evx-fondo{position:fixed;inset:0;background:rgba(11,18,21,.55);z-index:3000;display:flex;align-items:flex-start;justify-content:center;padding:4vh 16px;overflow:auto}
.evx{background:var(--ev-surface,#fff);color:var(--ev-text,#0B1215);border-radius:24px;box-shadow:0 30px 80px -30px rgba(11,18,21,.45);width:min(1100px,100%);padding:24px 26px;font-family:inherit}
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
.evx-nota.ok{background:rgba(43,212,196,.14)} .evx-nota.mal{background:rgba(229,72,77,.12)} .evx-nota.info{background:var(--ev-surface-2,#F5FBFA)}
.evx-tabla{width:100%;border-collapse:collapse;margin-top:14px;font-size:13.5px}
.evx-tabla th{text-align:left;font-size:12px;text-transform:uppercase;letter-spacing:.04em;color:var(--ev-muted,#66807C);padding:8px 10px;border-bottom:1px solid var(--ev-line,#E2EFED);position:sticky;top:0;background:var(--ev-surface,#fff)}
.evx-tabla td{padding:9px 10px;border-bottom:1px solid var(--ev-line,#E2EFED);vertical-align:top}
.evx-tabla tr.mal td{background:rgba(229,72,77,.06)}
.evx-tabla .est{font-weight:700;white-space:nowrap} .evx-tabla tr.mal .est{color:#C9373C} .evx-tabla tr:not(.mal) .est{color:var(--ev-deep,#0E8F86)}
.evx-lista{max-height:52vh;overflow:auto;border-radius:12px}
.evx-pie{display:flex;gap:10px;justify-content:flex-end;flex-wrap:wrap;margin-top:18px}
.evx-btn{display:inline-flex;align-items:center;gap:8px;border-radius:999px;padding:10px 18px;font-weight:700;font-size:14px;cursor:pointer;border:1px solid var(--ev-line,#E2EFED);background:var(--ev-surface,#fff);color:inherit;text-decoration:none}
.evx-btn.pri{background:var(--ev-ink,#0B1215);color:#fff;border-color:transparent}
.evx-btn[disabled]{opacity:.45;cursor:not-allowed}
.evx-filtro{display:flex;gap:6px;margin-top:12px}
.evx-filtro button{border:1px solid var(--ev-line,#E2EFED);background:none;border-radius:999px;padding:5px 12px;font-weight:700;font-size:12.5px;cursor:pointer;color:inherit}
.evx-filtro button.on{background:var(--ev-ink,#0B1215);color:#fff}
.evx-spin{width:18px;height:18px;border:3px solid var(--ev-line,#E2EFED);border-top-color:var(--ev-deep,#0E8F86);border-radius:50%;animation:evxg 1s linear infinite;flex:none}
@keyframes evxg{to{transform:rotate(360deg)}}`;
        document.head.appendChild(s);
    }

    async function enviar(url, archivo, guardar) {
        const fd = new FormData();
        fd.append('archivo', archivo);
        fd.append('guardar', guardar ? 'true' : 'false');
        const r = await fetch(url, { method: 'POST', body: fd, headers: { 'RequestVerificationToken': token() } });
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
                <div class="sub">Carga muchos ${esc(o.varios)} de una vez. Primero se revisa cada fila; luego guardas solo las correctas.</div></div>
                <button type="button" class="x" data-evx="cerrar" title="Cerrar"><i class="bi bi-x-lg"></i></button></div>
            <div class="evx-pasos">
                <div class="evx-paso"><b>1 · Descarga la plantilla</b>Trae ejemplos y una hoja «Instrucciones» con los valores válidos.</div>
                <div class="evx-paso"><b>2 · Llénala y súbela</b>Una fila por ${esc(o.uno)}. Borra las filas de ejemplo.</div>
                <div class="evx-paso"><b>3 · Revisa y guarda</b>Se guardan solo las filas correctas; las que fallan dicen por qué.</div>
            </div>
            <div style="display:flex;gap:10px;flex-wrap:wrap;margin-bottom:12px"><a class="evx-btn" href="${esc(o.plantilla)}"><i class="bi bi-download"></i> Descargar plantilla</a></div>
            <label class="evx-drop" data-evx="drop"><i class="bi bi-cloud-arrow-up"></i>
                <div><b style="font-size:15.5px">Arrastra aquí el Excel o haz clic para elegirlo</b><small>Archivo .xlsx con la misma forma que la plantilla</small></div>
                <input type="file" accept=".xlsx,.xlsm" hidden data-evx="archivo"></label>
            <div data-evx="resultado"></div>
            <div class="evx-pie"><button type="button" class="evx-btn" data-evx="cerrar">Cerrar</button>
                <button type="button" class="evx-btn pri" data-evx="guardar" disabled><i class="bi bi-cloud-check"></i> Guardar</button></div>
        </div>`;
        document.body.appendChild(fondo);
        const $ = s => fondo.querySelector(`[data-evx="${s}"]`);
        let archivo = null, guardado = false, ultimo = null, filtro = 'todas';

        const cerrar = () => { fondo.remove(); document.removeEventListener('keydown', tecla); if (guardado) o.alTerminar?.(); };
        const tecla = e => { if (e.key === 'Escape') cerrar(); };
        document.addEventListener('keydown', tecla);
        fondo.addEventListener('click', e => { if (e.target === fondo || e.target.closest('[data-evx="cerrar"]')) cerrar(); });

        function pintar(r) {
            ultimo = r;
            const filas = r.filas.filter(f => filtro === 'todas' || (filtro === 'mal' ? !f.ok : f.ok));
            const clase = r.guardado ? (r.conError ? 'info' : 'ok') : r.conError ? (r.correctas ? 'info' : 'mal') : 'ok';
            $('resultado').innerHTML = `<div class="evx-nota ${clase}"><i class="bi ${clase === 'mal' ? 'bi-x-circle-fill' : clase === 'ok' ? 'bi-check-circle-fill' : 'bi-info-circle-fill'}"></i><div>${esc(r.mensaje)}</div></div>
                <div class="evx-filtro"><button type="button" data-f="todas" class="${filtro === 'todas' ? 'on' : ''}">Todas (${r.filas.length})</button>
                    <button type="button" data-f="ok" class="${filtro === 'ok' ? 'on' : ''}">Correctas (${r.correctas})</button>
                    <button type="button" data-f="mal" class="${filtro === 'mal' ? 'on' : ''}">Con error (${r.conError})</button></div>
                <div class="evx-lista"><table class="evx-tabla"><thead><tr><th style="width:60px">Fila</th><th>${esc(o.uno[0].toUpperCase() + o.uno.slice(1))}</th><th>Datos</th><th style="width:34%">Estado</th></tr></thead><tbody>
                ${filas.map(f => `<tr class="${f.ok ? '' : 'mal'}"><td>${f.fila}</td><td><b>${esc(f.nombre || '—')}</b></td><td>${esc(f.detalle || '')}</td>
                    <td class="est">${f.ok ? (f.id ? `<i class="bi bi-check2-all"></i> Guardado n.º ${f.id}` : '<i class="bi bi-check2"></i> Correcta') : `<i class="bi bi-exclamation-triangle"></i> <span style="font-weight:600;white-space:normal">${esc(f.mensaje)}</span>`}</td></tr>`).join('')
                  || '<tr><td colspan="4" style="text-align:center;color:var(--ev-muted)">No hay filas con ese filtro.</td></tr>'}
                </tbody></table></div>`;
            fondo.querySelectorAll('.evx-filtro button').forEach(b => b.onclick = () => { filtro = b.dataset.f; pintar(ultimo); });
            const g = $('guardar');
            g.disabled = r.guardado || !r.correctas;
            g.innerHTML = r.guardado ? '<i class="bi bi-check2-all"></i> Guardado' : `<i class="bi bi-cloud-check"></i> Guardar ${r.correctas} ${r.correctas === 1 ? o.uno : o.varios}`;
        }

        async function leer(f) {
            if (!f) return;
            archivo = f; guardado = false; filtro = 'todas';
            $('guardar').disabled = true;
            $('resultado').innerHTML = `<div class="evx-nota info"><span class="evx-spin"></span><div>Revisando <b>${esc(f.name)}</b>… (todavía no se guarda nada)</div></div>`;
            try { pintar(await enviar(o.url, f, false)); }
            catch (e) { $('resultado').innerHTML = `<div class="evx-nota mal"><i class="bi bi-x-circle-fill"></i><div>${esc(e.message)}</div></div>`; }
            finally { $('archivo').value = ''; }
        }
        $('archivo').addEventListener('change', e => leer(e.target.files[0]));
        const drop = $('drop');
        ['dragenter', 'dragover'].forEach(t => drop.addEventListener(t, e => { e.preventDefault(); drop.classList.add('sobre'); }));
        ['dragleave', 'drop'].forEach(t => drop.addEventListener(t, e => { e.preventDefault(); drop.classList.remove('sobre'); }));
        drop.addEventListener('drop', e => leer(e.dataTransfer.files[0]));

        $('guardar').addEventListener('click', async () => {
            if (!archivo || !ultimo?.correctas) return;
            const n = ultimo.correctas;
            if (!confirm(`¿Guardar ${n} ${n === 1 ? o.uno : o.varios}?` + (ultimo.conError ? `\nLas ${ultimo.conError} fila(s) con error NO se guardan.` : ''))) return;
            const g = $('guardar');
            g.disabled = true; g.innerHTML = '<span class="evx-spin"></span> Guardando…';
            try { const r = await enviar(o.url, archivo, true); guardado = r.guardadas > 0; filtro = 'todas'; pintar(r); }
            catch (e) { $('resultado').insertAdjacentHTML('afterbegin', `<div class="evx-nota mal"><i class="bi bi-x-circle-fill"></i><div>${esc(e.message)}</div></div>`); pintar(ultimo); }
        });
    }
    return { abrir };
})();
