// Elegir una ubicación en el mapa (como /Destino/CreateOrUpdate de BackTiTan): ventana con buscador de Google (autocompletar),
// mapa, clic o arrastrar el marcador, y latitud/longitud en vivo. Sin clave de Google Maps se escriben a mano.
//   EVMapa.elegir({ lat, lng, buscar, titulo }) → Promise<{ lat, lng, nombre } | null>   (null = cancelado)
// La clave sale de window.EV_MAPS_KEY (GoogleMaps:ApiKey de appsettings, nunca escrita en el código). Usa EV (eurovip-ui.js).
window.EVMapa = (() => {
    const css = document.createElement('style');
    css.textContent = `
.evm-fondo { position: fixed; inset: 0; z-index: 3000; background: rgba(10,25,30,.45); display: grid; place-items: center; padding: 16px; animation: evmIn .15s ease-out; }
@keyframes evmIn { from { opacity: 0 } }
.evm { width: min(820px, 100%); max-height: calc(100vh - 32px); overflow: auto; background: var(--ev-surface, #fff); border-radius: 22px; box-shadow: 0 30px 80px -20px rgba(0,0,0,.45); padding: 18px; display: grid; gap: 12px; }
.evm, .evm * { box-sizing: border-box; }
.evm h3 { margin: 0; font-size: 18px; font-weight: 800; display: flex; align-items: center; gap: 8px; }
.evm .evm-x { margin-left: auto; border: 0; background: none; font-size: 20px; cursor: pointer; color: var(--ev-muted, #66807C); }
.evm input { width: 100%; border: 1.5px solid var(--ev-line, #DCE7E5); border-radius: 12px; padding: 10px 12px; font: 600 14px var(--ev-font, system-ui); background: #fff; }
.evm input:focus { outline: 0; border-color: #0E8F86; box-shadow: 0 0 0 3px rgba(14,143,134,.18); }
.evm .evm-mapa { height: 400px; border-radius: 16px; overflow: hidden; background: #EEF5F4; display: grid; place-items: center; color: var(--ev-muted, #66807C); font-weight: 700; text-align: center; padding: 0; }
.evm .evm-coord { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.evm label { display: block; font: 800 12.5px var(--ev-font, system-ui); margin-bottom: 5px; }
.evm .evm-pie { display: flex; gap: 8px; justify-content: flex-end; align-items: center; flex-wrap: wrap; }
.evm .evm-pie small { margin-right: auto; color: var(--ev-muted, #66807C); font-weight: 600; }
.pac-container { z-index: 3100 !important; }
@media (max-width: 640px) { .evm .evm-mapa { height: 300px; } .evm .evm-coord { grid-template-columns: 1fr; } }`;
    document.head.appendChild(css);

    let promesa = null;
    function cargar() {
        if (window.google?.maps?.Map) return Promise.resolve(google.maps);
        if (!window.EV_MAPS_KEY) return Promise.reject(new Error('Falta la clave de Google Maps (GoogleMaps:ApiKey en appsettings). Escribe la latitud y la longitud a mano.'));
        promesa ??= new Promise((ok, mal) => {
            window.__evMapaListo = () => ok(google.maps);
            window.gm_authFailure = () => EV.toast('Google Maps no aceptó la clave (puede estar limitada a otro dominio). Escribe la latitud y la longitud a mano.', 'err');
            const s = document.createElement('script');
            s.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(window.EV_MAPS_KEY)}&libraries=places&language=es&callback=__evMapaListo`;
            s.async = true;
            s.onerror = () => { promesa = null; mal(new Error('No se pudo cargar Google Maps (revisa la conexión). Escribe la latitud y la longitud a mano.')); };
            document.head.appendChild(s);
        });
        return promesa;
    }

    const num = v => { const n = parseFloat(String(v ?? '').replace(',', '.')); return isFinite(n) ? n : null; };
    const fijo = n => Math.round(n * 1e6) / 1e6;

    function elegir({ lat = null, lng = null, buscar = '', titulo = 'Ubicación' } = {}) {
        return new Promise(resolve => {
            const fondo = document.createElement('div');
            fondo.className = 'evm-fondo';
            fondo.innerHTML = `<div class="evm" role="dialog" aria-modal="true" aria-label="${EV.esc(titulo)}">
                <h3><i class="bi bi-geo-alt-fill" style="color:#0E8F86"></i> ${EV.esc(titulo)}<button type="button" class="evm-x" data-cerrar aria-label="Cerrar"><i class="bi bi-x-lg"></i></button></h3>
                <div><label for="evmBuscar">Buscar el lugar</label><input id="evmBuscar" placeholder="Ej. Punta Cana, República Dominicana" autocomplete="off" value="${EV.esc(buscar)}"></div>
                <div class="evm-mapa" id="evmMapa">Cargando el mapa…</div>
                <div class="evm-coord">
                    <div><label for="evmLat">Latitud</label><input id="evmLat" inputmode="decimal" placeholder="Ej. 18.582010" value="${lat ?? ''}"></div>
                    <div><label for="evmLng">Longitud</label><input id="evmLng" inputmode="decimal" placeholder="Ej. -68.405473" value="${lng ?? ''}"></div>
                </div>
                <div class="evm-pie"><small>Busca el lugar, pulsa en el mapa o arrastra el marcador.</small>
                    <button type="button" class="ev-btn ghost" data-cerrar>Cancelar</button>
                    <button type="button" class="ev-btn" data-aceptar><i class="bi bi-check2-circle"></i> Usar esta ubicación</button></div>
            </div>`;
            document.body.appendChild(fondo);
            const $ = s => fondo.querySelector(s);
            let nombre = null, mapa = null, marca = null;
            const fin = r => { fondo.remove(); document.removeEventListener('keydown', tecla); resolve(r); };
            const tecla = e => { if (e.key === 'Escape') fin(null); };
            document.addEventListener('keydown', tecla);
            fondo.addEventListener('click', e => { if (e.target === fondo || e.target.closest('[data-cerrar]')) fin(null); });
            $('[data-aceptar]').onclick = () => {
                const la = num($('#evmLat').value), lo = num($('#evmLng').value);
                if (la == null || lo == null || Math.abs(la) > 90 || Math.abs(lo) > 180) { EV.toast('Elige un punto en el mapa o escribe una latitud (-90 a 90) y una longitud (-180 a 180).', 'err'); return; }
                fin({ lat: fijo(la), lng: fijo(lo), nombre });
            };
            const poner = (la, lo, centrar) => {
                $('#evmLat').value = fijo(la); $('#evmLng').value = fijo(lo);
                if (!mapa) return;
                const p = { lat: la, lng: lo };
                marca.setPosition(p); marca.setVisible(true);
                if (centrar) { mapa.setCenter(p); if (mapa.getZoom() < 9) mapa.setZoom(11); }
            };
            setTimeout(() => $('#evmBuscar').focus(), 50);
            cargar().then(gm => {
                const la = num(lat), lo = num(lng), hay = la != null && lo != null;
                $('#evmMapa').textContent = '';
                mapa = new gm.Map($('#evmMapa'), { center: hay ? { lat: la, lng: lo } : { lat: 18.7357, lng: -70.1627 }, zoom: hay ? 11 : 7, mapTypeControl: false, streetViewControl: false });
                marca = new gm.Marker({ map: mapa, draggable: true, visible: hay, position: hay ? { lat: la, lng: lo } : null });
                marca.addListener('dragend', e => poner(e.latLng.lat(), e.latLng.lng(), false));
                mapa.addListener('click', e => poner(e.latLng.lat(), e.latLng.lng(), false));
                const auto = new gm.places.Autocomplete($('#evmBuscar'), { fields: ['geometry', 'name', 'formatted_address'] });
                auto.addListener('place_changed', () => {
                    const pl = auto.getPlace();
                    if (!pl.geometry?.location) { EV.toast('Elige un lugar de la lista.', 'err'); return; }
                    nombre = pl.name || pl.formatted_address || null;
                    if (pl.geometry.viewport) mapa.fitBounds(pl.geometry.viewport);
                    poner(pl.geometry.location.lat(), pl.geometry.location.lng(), !pl.geometry.viewport);
                });
                // sin coordenadas pero con nombre (ej. el destino escrito): se busca solo al abrir
                if (!hay && buscar.trim()) new gm.Geocoder().geocode({ address: buscar }, (r, est) => {
                    if (est === 'OK' && r[0]) { const g = r[0].geometry; if (g.viewport) mapa.fitBounds(g.viewport); poner(g.location.lat(), g.location.lng(), !g.viewport); }
                });
                ['#evmLat', '#evmLng'].forEach(s => $(s).addEventListener('change', () => { const a = num($('#evmLat').value), b = num($('#evmLng').value); if (a != null && b != null) poner(a, b, true); }));
            }).catch(e => { $('#evmMapa').textContent = e.message; $('#evmMapa').style.padding = '20px'; $('#evmMapa').style.height = 'auto'; });
        });
    }

    return { cargar, elegir };
})();
