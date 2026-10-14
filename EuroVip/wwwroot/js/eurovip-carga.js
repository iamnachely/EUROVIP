// =====================================================================
// EUROVIP — piezas comunes de la CARGA DE CIRCUITOS (Express y Clásica)
//   EVC.catalogos()                    carga una vez campañas, tasas, categorías, monedas, países, mínimo
//   EVC.vuelos(cont, {tipo})           constructor de vuelos (ida o vuelta)
//   EVC.calendario(cont, cfg)          calendario visual de fechas de salida
//   EVC.filas(cont, cfg)               tabla de filas (campañas / suplementos)
// =====================================================================
(function () {
    const EVC = {};
    let cache = null;

    EVC.catalogos = async () => {
        if (cache) return cache;
        const [campanyas, tasas, categorias, monedas, paises, minimo, regimenes] = await Promise.all([
            EV.api('/Circuitos/Campanyas'), EV.api('/Circuitos/Tasas'), EV.api('/Circuitos/Categorias'),
            EV.api('/Circuitos/Monedas'), EV.api('/Circuitos/Paises'), EV.api('/Circuitos/PrecioMinimo'), EV.api('/Circuitos/Regimenes')
        ]);
        cache = { campanyas, tasas, categorias, monedas, paises, minimo, regimenes };
        return cache;
    };

    // ------------------------------------------------------------------ VUELOS
    const CLASES = [['T', 'Turista'], ['Y', 'Económica'], ['P', 'Premium'], ['C', 'Business'], ['J', 'Business (J)'], ['F', 'Primera']];

    EVC.vuelos = (cont, cfg = {}) => {
        const tipo = cfg.tipo || 'ida';
        let n = 0;
        cont.innerHTML = `<div class="segs"></div>
            <button type="button" class="ev-btn ghost sm" data-add><i class="bi bi-plus-lg"></i> Agregar vuelo (${tipo === 'ida' ? 'ida' : 'vuelta'})</button>`;
        const segs = cont.querySelector('.segs');

        const agregar = (v = {}) => {
            n++;
            const s = document.createElement('div');
            s.className = 'ui-seg';
            s.innerHTML = `
                <div class="ui-seg-h">
                    <span class="tag">${tipo === 'ida' ? 'IDA' : 'VUELTA'} · <span data-num></span></span>
                    <span class="ruta" data-ruta>— → —</span>
                    <button type="button" class="ui-ibtn danger" data-del title="Quitar este vuelo"><i class="bi bi-trash3"></i></button>
                </div>
                <div class="ui-row r3">
                    <div class="ui-f"><label>Aerolínea</label><input data-k="aerolinea" placeholder="Busca la aerolínea…"></div>
                    <div class="ui-f"><label>Aeropuerto salida</label><input data-k="origen" placeholder="MAD, Madrid…"></div>
                    <div class="ui-f"><label>Aeropuerto llegada</label><input data-k="destino" placeholder="PUJ, Punta Cana…"></div>
                </div>
                <div class="ui-flight-line"><i class="bi bi-airplane"></i> <span data-dur>horario</span></div>
                <div class="ui-row r4">
                    <div class="ui-f"><label>Hora salida</label><input type="time" data-k="hs"></div>
                    <div class="ui-f"><label>Hora llegada</label><input type="time" data-k="hl"></div>
                    <div class="ui-f"><label>Nº de vuelo <em>opcional</em></label><input data-k="num" maxlength="15" placeholder="UX093"></div>
                    <div class="ui-f"><label>Clase</label><select data-k="clase">${CLASES.map(([c, t]) => `<option value="${c}">${c} · ${t}</option>`).join('')}</select></div>
                </div>
                <label class="ui-switch" style="margin:0 0 12px"><input type="checkbox" data-k="conexion"><span class="tr"></span> Es una conexión (escala)</label>`;
            segs.appendChild(s);

            const campo = k => s.querySelector(`[data-k="${k}"]`);
            const fijar = (k, valor, texto) => { const i = campo(k); i.dataset.valor = valor || ''; i.value = texto || ''; i.closest('.ui-f').classList.remove('err'); actualizar(); };

            EV.picker(campo('aerolinea'), {
                url: q => `/Circuitos/Proveedores?tipo=Aerolinea&q=${q}`, limpiar: false,
                map: x => ({ valor: x.valor, texto: x.texto, sub: x.valor, icono: 'bi-airplane' }),
                onSelect: m => fijar('aerolinea', m.valor, m.texto)
            });
            for (const k of ['origen', 'destino'])
                EV.picker(campo(k), {
                    url: q => `/Circuitos/Aeropuertos?q=${q}`, limpiar: false,
                    map: x => ({ valor: x.valor, texto: `${x.valor} · ${x.extra}`, sub: x.texto, icono: 'bi-geo-alt' }),
                    onSelect: m => fijar(k, m.valor, m.texto)
                });
            for (const k of ['aerolinea', 'origen', 'destino'])
                campo(k).addEventListener('input', () => { campo(k).dataset.valor = ''; actualizar(); });

            if (v.aerolinea) fijar('aerolinea', v.aerolinea, v.aerolineaTexto || v.aerolinea);
            if (v.aeropuertoOrigen) fijar('origen', v.aeropuertoOrigen, v.aeropuertoOrigen);
            if (v.aeropuertoDestino) fijar('destino', v.aeropuertoDestino, v.aeropuertoDestino);
            campo('hs').value = v.horaSalida || '';
            campo('hl').value = v.horaLlegada || '';
            campo('num').value = v.numeroVuelo || '';
            campo('clase').value = v.clase || 'T';
            campo('conexion').checked = !!v.conexion;

            s.addEventListener('input', actualizar);
            s.querySelector('[data-del]').onclick = () => { s.remove(); renumerar(); cfg.onChange?.(); };
            renumerar();
            actualizar();
            cfg.onChange?.();
            return s;

            function actualizar() {
                const o = campo('origen').dataset.valor, d = campo('destino').dataset.valor;
                s.querySelector('[data-ruta]').textContent = `${o || '—'} → ${d || '—'}`;
                const hs = campo('hs').value, hl = campo('hl').value;
                let txt = 'horario';
                if (hs && hl) {
                    // Horas locales de cada aeropuerto (no se calcula duración: hay cambio de zona horaria)
                    txt = `sale ${hs} → llega ${hl} (hora local)${hl < hs ? ' · llega al día siguiente' : ''}`;
                }
                s.querySelector('[data-dur]').textContent = txt;
                cfg.onChange?.();
            }
        };

        const renumerar = () => [...segs.children].forEach((s, i) => s.querySelector('[data-num]').textContent = `vuelo ${i + 1}`);
        cont.querySelector('[data-add]').onclick = () => {
            // El nuevo vuelo sale del aeropuerto donde llegó el anterior
            const prev = segs.lastElementChild;
            const desde = prev?.querySelector('[data-k="destino"]').dataset.valor;
            agregar(desde ? { aeropuertoOrigen: desde } : {});
        };

        return {
            agregar,
            limpiar() { segs.innerHTML = ''; },
            cantidad: () => segs.children.length,
            datos() {
                return [...segs.children].map(s => {
                    const c = k => s.querySelector(`[data-k="${k}"]`);
                    return {
                        Aerolinea: c('aerolinea').dataset.valor || '', AeropuertoOrigen: c('origen').dataset.valor || '',
                        AeropuertoDestino: c('destino').dataset.valor || '', NumeroVuelo: c('num').value.trim() || null,
                        Clase: c('clase').value, Conexion: c('conexion').checked, HoraSalida: c('hs').value, HoraLlegada: c('hl').value
                    };
                });
            },
            validar() {
                const errores = [];
                [...segs.children].forEach((s, i) => {
                    const pedir = (k, msg) => { const el = s.querySelector(`[data-k="${k}"]`); const vacio = k === 'hs' || k === 'hl' ? !el.value : !el.dataset.valor; el.closest('.ui-f').classList.toggle('err', vacio); if (vacio) errores.push(`${tipo === 'ida' ? 'Ida' : 'Vuelta'} vuelo ${i + 1}: ${msg}`); };
                    pedir('aerolinea', 'elige la aerolínea de la lista'); pedir('origen', 'elige el aeropuerto de salida');
                    pedir('destino', 'elige el aeropuerto de llegada'); pedir('hs', 'falta la hora de salida'); pedir('hl', 'falta la hora de llegada');
                });
                return errores;
            }
        };
    };

    // ------------------------------------------------------------------ CALENDARIO DE SALIDAS
    // cfg: { obtener: () => Map(fecha→precio), alternar: (fecha) => {}, meses: 6 }
    EVC.calendario = (cont, cfg) => {
        const DIAS = ['L', 'M', 'X', 'J', 'V', 'S', 'D'];
        const pintar = () => {
            const mapa = cfg.obtener();
            const fechas = [...mapa.keys()].sort();
            const hoy = EV.hoy();
            const inicio = new Date((fechas[0] || hoy) + 'T00:00:00');
            const ultimo = new Date((fechas[fechas.length - 1] || hoy) + 'T00:00:00');
            const meses = Math.max(cfg.meses || 3, (ultimo.getFullYear() - inicio.getFullYear()) * 12 + ultimo.getMonth() - inicio.getMonth() + 1);
            let html = '';
            for (let m = 0; m < Math.min(meses, 24); m++) {
                const primero = new Date(inicio.getFullYear(), inicio.getMonth() + m, 1);
                const dias = new Date(primero.getFullYear(), primero.getMonth() + 1, 0).getDate();
                const hueco = (primero.getDay() + 6) % 7;
                const enMes = fechas.filter(f => f.startsWith(EV.iso(primero).slice(0, 7))).length;
                const mes = primero.toLocaleDateString('es-ES', { month: 'long', year: 'numeric' });
                html += `<div class="ui-month"><h4>${mes.charAt(0).toUpperCase() + mes.slice(1)}<small>${enMes ? enMes + ' salida' + (enMes > 1 ? 's' : '') : ''}</small></h4><div class="g">`;
                html += DIAS.map(d => `<span>${d}</span>`).join('') + '<span></span>'.repeat(hueco);
                for (let d = 1; d <= dias; d++) {
                    const f = EV.iso(new Date(primero.getFullYear(), primero.getMonth(), d));
                    const sel = mapa.has(f);
                    const p = mapa.get(f);
                    html += `<button type="button" data-f="${f}" class="${sel ? 'sel' : ''} ${f === hoy ? 'hoy' : ''}" ${f < hoy ? 'disabled' : ''} title="${sel ? 'Quitar' : 'Agregar'} ${EV.fecha(f)}">${d}${sel && p ? `<em>${EV.fmt(p)}</em>` : ''}</button>`;
                }
                html += '</div></div>';
            }
            cont.innerHTML = `<div class="ui-cal">${html}</div>`;
        };
        cont.addEventListener('click', e => { const b = e.target.closest('[data-f]'); if (b && !b.disabled) { cfg.alternar(b.dataset.f); } });
        pintar();
        return { pintar };
    };

    // ------------------------------------------------------------------ FILAS (campañas / suplementos)
    // cfg: { opciones: [{valor,texto,extra}], etiqueta, valorEtiqueta, sufijo, inicial: [{opcion, valor}], onChange, placeholder }
    EVC.filas = (cont, cfg) => {
        cont.innerHTML = `<div class="ui-table-wrap"><table class="ui-table"><thead><tr><th>${cfg.etiqueta}</th><th style="width:170px">${cfg.valorEtiqueta}</th><th style="width:52px"></th></tr></thead><tbody></tbody></table></div>
            <button type="button" class="ev-btn ghost sm" data-add style="margin-top:10px"><i class="bi bi-plus-lg"></i> Agregar</button>`;
        const tb = cont.querySelector('tbody');
        const opts = sel => `<option value="">${cfg.placeholder || 'Elige…'}</option>` + cfg.opciones.map(o => `<option value="${EV.esc(o.valor)}" ${String(o.valor) === String(sel) ? 'selected' : ''}>${EV.esc(o.texto)}${o.extra ? ' · ' + EV.esc(o.extra) : ''}</option>`).join('');
        const agregar = (opcion = '', valor = '') => {
            const tr = document.createElement('tr');
            tr.innerHTML = `<td><select data-o>${opts(opcion)}</select></td>
                <td><div class="ui-money"><b>${cfg.sufijo || ''}</b><input data-v type="number" step="0.01" min="0" value="${valor}"></div></td>
                <td><button type="button" class="ui-ibtn danger" title="Quitar"><i class="bi bi-trash3"></i></button></td>`;
            tr.querySelector('button').onclick = () => { tr.remove(); cfg.onChange?.(); };
            tr.addEventListener('input', () => cfg.onChange?.());
            tr.addEventListener('change', () => cfg.onChange?.());
            tb.appendChild(tr);
            cfg.onChange?.();
        };
        cont.querySelector('[data-add]').onclick = () => agregar();
        (cfg.inicial || []).forEach(x => agregar(x.opcion, x.valor));
        return {
            agregar,
            limpiar() { tb.innerHTML = ''; cfg.onChange?.(); },
            datos: () => [...tb.children].map(tr => ({ opcion: tr.querySelector('[data-o]').value, valor: parseFloat(tr.querySelector('[data-v]').value) || 0 })).filter(x => x.opcion),
            total: () => [...tb.children].reduce((s, tr) => s + (tr.querySelector('[data-o]').value ? parseFloat(tr.querySelector('[data-v]').value) || 0 : 0), 0)
        };
    };

    // ------------------------------------------------------------------ BLOQUE DE HOTEL (ofertas)
    // Hotel + régimen + habitaciones (con suplemento opcional). cfg: { regimenes, onChange, onQuitar, hotel: {id, nombre, destino}, regimen, habitaciones,
    //   destino: () => 'Punta Cana' | null }  ← destino que ya fijaron los OTROS hoteles de la carga: la búsqueda solo ofrece ese destino.
    EVC.hotelOferta = (cont, cfg) => {
        const caja = document.createElement('div');
        caja.className = 'ui-seg';
        caja.innerHTML = `
            <div class="ui-seg-h"><span class="tag"><i class="bi bi-building"></i> HOTEL</span><span class="ruta" data-nombre>Elige el hotel</span>
                <button type="button" class="ui-ibtn danger" data-del title="Quitar hotel"><i class="bi bi-trash3"></i></button></div>
            <div class="ui-row">
                <div class="ui-f"><label>Hotel</label><input data-k="hotel" placeholder="Busca por nombre o número…"><span class="msg"></span></div>
                <div class="ui-f"><label>Régimen</label><select data-k="regimen">${cfg.regimenes.map(r => `<option value="${EV.esc(r.valor)}">${EV.esc(r.valor)} · ${EV.esc(r.texto)}</option>`).join('')}</select></div>
            </div>
            <div data-habs></div>`;
        cont.appendChild(caja);
        const $ = s => caja.querySelector(s);
        let hotel = null, habitaciones = [];
        if (cfg.regimen) $('[data-k=regimen]').value = cfg.regimen; else { const ti = cfg.regimenes.find(r => r.valor === 'TI'); if (ti) $('[data-k=regimen]').value = 'TI'; }

        const pintarHabs = (marcadas) => {
            if (!hotel) { $('[data-habs]').innerHTML = ''; return; }
            if (!habitaciones.length) { $('[data-habs]').innerHTML = '<div class="ui-note warn"><i class="bi bi-exclamation-triangle"></i><div>Este hotel no tiene habitaciones cargadas en Eurovip.</div></div>'; return; }
            const muchas = habitaciones.length > 8;
            $('[data-habs]').innerHTML = `<div class="ui-f" style="margin:0"><label>Habitaciones <em>marca las que se venden · suplemento sobre el precio (0 = habitación base)</em></label></div>
                ${muchas ? `<div style="display:flex;gap:10px;align-items:center;margin-bottom:10px;flex-wrap:wrap">
                    <label class="ui-search" style="flex:1;min-width:200px"><i class="bi bi-search"></i><input type="search" data-filtro placeholder="Filtrar ${habitaciones.length} habitaciones (doble, suite, vista mar…)"></label>
                    <label class="ui-switch"><input type="checkbox" data-solo><span class="tr"></span> Solo marcadas</label>
                    <b data-cuenta style="font-size:13px;color:var(--ev-muted)"></b></div>` : ''}
                <div class="ui-table-wrap" style="max-height:260px;overflow:auto"><table class="ui-table"><thead><tr><th style="width:44px"></th><th>Habitación</th><th>Tipo</th><th style="width:160px">Suplemento</th></tr></thead><tbody>
                ${habitaciones.map((h, i) => { const m = marcadas?.find(x => x.id_Habitacion === h.id_Habitacion || x.Id_Habitacion === h.id_Habitacion); const on = marcadas ? !!m : i === 0;
                    return `<tr data-txt="${EV.esc(`${h.nombre} ${h.id_Tipo_Habitacion || ''} ${h.id_Habitacion}`.toLowerCase())}"><td><input type="checkbox" data-hab="${h.id_Habitacion}" ${on ? 'checked' : ''} style="width:18px;height:18px;min-width:0;accent-color:var(--ev-ink)"></td>
                    <td><b>${EV.esc(h.nombre)}</b> <small style="color:var(--ev-muted)">#${h.id_Habitacion}</small></td><td>${EV.esc(h.id_Tipo_Habitacion || '')}</td>
                    <td><div class="ui-money"><b>+</b><input type="number" min="0" step="1" data-sup="${h.id_Habitacion}" value="${m ? (m.precio ?? m.Precio ?? 0) : 0}"></div></td></tr>`; }).join('')}
                </tbody></table></div>`;
            filtrar();
        };

        // Filtro de habitaciones (hoteles con muchas): las marcadas siempre se ven salvo que no coincidan con "solo marcadas"
        const filtrar = () => {
            const f = $('[data-filtro]'); if (!f) return;
            const q = f.value.trim().toLowerCase(), solo = $('[data-solo]').checked;
            caja.querySelectorAll('tbody tr[data-txt]').forEach(tr => {
                const on = tr.querySelector('[data-hab]').checked;
                tr.hidden = solo ? !on : !(on || !q || tr.dataset.txt.includes(q));
            });
            $('[data-cuenta]').textContent = `${caja.querySelectorAll('[data-hab]:checked').length} marcada(s)`;
        };
        caja.addEventListener('input', e => { if (e.target.matches('[data-filtro]')) filtrar(); });
        caja.addEventListener('change', e => { if (e.target.matches('[data-solo], [data-hab]')) filtrar(); });

        const elegir = async (id, nombre, destino, marcadas) => {
            hotel = { id: +id, nombre, destino };
            $('[data-k=hotel]').value = nombre;
            $('[data-k=hotel]').closest('.ui-f').classList.remove('err');
            $('[data-nombre]').textContent = `${nombre}${destino ? ' · ' + destino : ''}`;
            try { habitaciones = await EV.api('/Ofertas/Habitaciones?hotel=' + hotel.id); } catch { habitaciones = []; }
            pintarHabs(marcadas);
            cfg.onChange?.();
        };

        // Todos los hoteles de una carga son del mismo destino (el destino de la oferta).
        const fijo = () => (cfg.destino?.() || '').trim();
        EV.picker($('[data-k=hotel]'), {
            url: q => `/Circuitos/Hoteles?q=${q}` + (fijo() ? `&destino=${encodeURIComponent(fijo())}` : ''), limpiar: false,
            map: x => ({ valor: x.id_Alojamiento, texto: x.nombre, sub: `${x.destino} · #${x.id_Alojamiento}`, img: EV.esUrl(x.caratula) ? x.caratula : null, icono: 'bi-building' }),
            onSelect: (m, raw) => {
                const d = fijo();
                if (d && (raw.destino || '').trim().toLowerCase() !== d.toLowerCase()) {
                    EV.toast(`${raw.nombre} es de ${raw.destino || 'otro destino'}. Esta carga es de ${d}: todos los hoteles tienen que ser del mismo destino.`, 'err');
                    $('[data-k=hotel]').value = ''; return;
                }
                elegir(raw.id_Alojamiento, raw.nombre, raw.destino);
            }
        });
        const pista = () => { const d = fijo(); $('[data-k=hotel]').placeholder = d ? `Busca hoteles de ${d}…` : 'Busca por nombre o número…'; };
        $('[data-k=hotel]').addEventListener('focus', pista); pista();
        $('[data-k=hotel]').addEventListener('input', () => { hotel = null; $('[data-nombre]').textContent = 'Elige el hotel'; pintarHabs(); cfg.onChange?.(); });
        caja.addEventListener('change', () => cfg.onChange?.());
        $('[data-del]').onclick = () => { caja.remove(); cfg.onQuitar?.(); cfg.onChange?.(); };
        if (cfg.hotel) elegir(cfg.hotel.id, cfg.hotel.nombre, cfg.hotel.destino, cfg.habitaciones);

        return {
            el: caja,
            hotel: () => hotel,
            datos: () => !hotel ? null : {
                Id_Alojamiento: hotel.id, Nombre: hotel.nombre, Id_Regimen: $('[data-k=regimen]').value,
                Habitaciones: [...caja.querySelectorAll('[data-hab]:checked')].map(c => ({ Id_Habitacion: +c.dataset.hab, Precio: parseFloat(caja.querySelector(`[data-sup="${c.dataset.hab}"]`).value) || 0 }))
            },
            validar() {
                const e = [];
                if (!hotel) { $('[data-k=hotel]').closest('.ui-f').classList.add('err'); e.push('Elige el hotel de la lista'); }
                else if (!caja.querySelector('[data-hab]:checked')) e.push(`${hotel.nombre}: marca al menos una habitación`);
                return e;
            }
        };
    };

    /** Destino que fijan los hoteles ya elegidos (sin contar `excepto`). Si están mezclados, devuelve el primero. */
    EVC.destinoDe = (bloques, excepto) => (bloques.filter(b => b !== excepto).map(b => b.hotel()?.destino).find(d => d && d.trim()) || '').trim() || null;
    /** Destinos distintos entre los hoteles elegidos (para avisar si se mezclaron). */
    EVC.destinosDe = bloques => [...new Map(bloques.map(b => b.hotel()?.destino?.trim()).filter(Boolean).map(d => [d.toLowerCase(), d])).values()];

    // ------------------------------------------------------------------ MATRIZ DE PRECIOS hotel × fecha (ofertas)
    // Filas = fechas de entrada, columnas = hoteles. Celda vacía = ese hotel no se vende esa fecha.
    // cfg: { hoteles: () => [{uid, nombre}], noches: () => n, moneda: () => 'EUR', tasas: () => total, minimo, onChange }
    EVC.matriz = (cont, cfg) => {
        const fechas = new Set();
        const precios = new Map();             // `${uid}|${fecha}` → precio total
        const k = (uid, f) => `${uid}|${f}`;
        const ordenadas = () => [...fechas].sort();
        const cambio = () => cfg.onChange?.();

        const pintar = () => {
            const hs = cfg.hoteles(), fs = ordenadas(), n = cfg.noches(), m = cfg.moneda(), t = cfg.tasas();
            if (!hs.length) { cont.innerHTML = '<div class="ui-empty" style="padding:22px"><p style="margin:0">Primero agrega al menos un hotel.</p></div>'; return; }
            if (!fs.length) { cont.innerHTML = '<div class="ui-empty" style="padding:22px"><p style="margin:0">Sin fechas. Usa el generador o toca días en el calendario.</p></div>'; return; }
            const sim = EV.simbolo(m);
            cont.innerHTML = `<div class="ui-table-wrap" style="max-height:520px;overflow:auto"><table class="ui-table"><thead><tr>
                <th style="min-width:170px">Entrada → salida</th>
                ${hs.map(h => `<th style="min-width:170px"><div style="font-size:12.5px;line-height:1.25;margin-bottom:6px;text-transform:none;letter-spacing:0">${EV.esc(h.nombre || 'Hotel sin elegir')}</div>
                    <div class="ui-money"><b>${sim}</b><input type="number" min="0" step="1" placeholder="Todas" data-col="${h.uid}" title="Precio para todas las fechas de este hotel"></div></th>`).join('')}
                <th style="width:52px"></th></tr></thead><tbody>
                ${fs.map(f => `<tr><td><b>${EV.fechaCorta(f)}</b> <small style="color:var(--ev-muted)">${f.slice(0, 4)}</small><div style="color:var(--ev-muted);font-size:12px;font-weight:600">${n ? 'hasta ' + EV.fechaCorta(EV.sumarDias(f, n)) : ''}</div></td>
                    ${hs.map(h => { const p = precios.get(k(h.uid, f)); const bad = p > 0 && p - t < cfg.minimo;
                        return `<td style="${bad ? 'background:#FFFBFA' : ''}"><div class="ui-money"><b>${sim}</b><input type="number" min="0" step="1" value="${p || ''}" data-h="${h.uid}" data-f="${f}" placeholder="no se vende"></div>
                        ${bad ? `<small style="color:#D92D20;font-weight:700"><i class="bi bi-exclamation-triangle"></i> base ${EV.fmt(p - t)} &lt; mínimo</small>` : ''}</td>`; }).join('')}
                    <td><button type="button" class="ui-ibtn danger" data-quitar="${f}" title="Quitar fecha"><i class="bi bi-x-lg"></i></button></td></tr>`).join('')}
                </tbody></table></div>`;
        };

        cont.addEventListener('input', e => {
            const i = e.target;
            if (i.dataset.h) { const v = parseFloat(i.value); if (v > 0) precios.set(k(i.dataset.h, i.dataset.f), v); else precios.delete(k(i.dataset.h, i.dataset.f)); cambio(); }
        });
        cont.addEventListener('change', e => {
            const i = e.target;
            if (i.dataset.col) { const v = parseFloat(i.value); if (v > 0) fechas.forEach(f => precios.set(k(i.dataset.col, f), v)); pintar(); cambio(); }
            else if (i.dataset.h) pintar();
        });
        cont.addEventListener('click', e => { const b = e.target.closest('[data-quitar]'); if (b) api.quitar(b.dataset.quitar); });

        const api = {
            pintar,
            fechas: ordenadas,
            tiene: f => fechas.has(f),
            // agrega la fecha; si viene precio lo pone a los hoteles que aún no tienen precio esa fecha
            agregar(f, precio) {
                fechas.add(f);
                if (precio > 0) cfg.hoteles().forEach(h => { if (!precios.has(k(h.uid, f))) precios.set(k(h.uid, f), precio); });
            },
            fijar(uid, f, p) { fechas.add(f); if (p > 0) precios.set(k(uid, f), p); else precios.delete(k(uid, f)); },
            quitar(f) { fechas.delete(f); [...precios.keys()].filter(x => x.endsWith('|' + f)).forEach(x => precios.delete(x)); pintar(); cambio(); },
            quitarHotel(uid) { [...precios.keys()].filter(x => x.startsWith(uid + '|')).forEach(x => precios.delete(x)); },
            limpiar() { fechas.clear(); precios.clear(); pintar(); cambio(); },
            todas(p) { cfg.hoteles().forEach(h => fechas.forEach(f => precios.set(k(h.uid, f), p))); pintar(); cambio(); },
            salidas: uid => ordenadas().filter(f => precios.get(k(uid, f)) > 0).map(f => ({ Desde: f, PrecioTotal: precios.get(k(uid, f)) })),
            precios: () => cfg.hoteles().flatMap(h => api.salidas(h.uid).map(s => s.PrecioTotal)),
            // fecha → precio más bajo (para el calendario)
            mapa: () => new Map(ordenadas().map(f => { const ps = cfg.hoteles().map(h => precios.get(k(h.uid, f))).filter(x => x > 0); return [f, ps.length ? Math.min(...ps) : 0]; })),
            errores() {
                const e = [], hs = cfg.hoteles(), t = cfg.tasas();
                if (!fechas.size) e.push('Agrega al menos una fecha de entrada.');
                hs.forEach(h => { if (fechas.size && !api.salidas(h.uid).length) e.push(`${h.nombre || 'Un hotel'}: no tiene ningún precio.`); });
                const bajas = api.precios().filter(p => p - t < cfg.minimo).length;
                if (bajas) e.push(`${bajas} precio${bajas > 1 ? 's quedan' : ' queda'} con base menor al mínimo (${EV.fmt(cfg.minimo)}).`);
                return e;
            }
        };
        return api;
    };

    window.EVC = EVC;
})();
