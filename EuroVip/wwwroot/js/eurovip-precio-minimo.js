// CAMBIO 139 · Precio mínimo de carga por vista (Hoteles, Solo Hotel, Paquetes, Circuitos, Excursiones, Traslados, Cruceros).
// Pedido de la usuaria: «en cada vista de carga un botón moderno para establecer el precio mínimo con que se podrá cargar;
// si no se pone nada no hay mínimo; si se pone, avisa y no deja cargar por debajo, pero deja editar y ajustar el precio».
//   • <span data-precio-minimo="TRASLADOS"></span> en la cabecera de la vista → se convierte en el botón con el valor actual.
//   • EVPrecioMin.valor('TRASLADOS') → Promise<number|null> (null = sin mínimo).
//   • EVPrecioMin.revisar('TRASLADOS', [{ precio, que }]) → true si todo está bien; si no, muestra el aviso y devuelve false.
//   • Si el servidor rechaza un precio (respuesta con «precioMinimo»), el aviso sale solo (no hay que tocar cada pantalla).
// No depende de eurovip-ui.js (Hoteles y Traslados no lo cargan): estilos, modal y token propios.
window.EVPrecioMin = (() => {
    const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const cifra = n => Number(n).toLocaleString('es-DO', { minimumFractionDigits: n % 1 ? 2 : 0, maximumFractionDigits: 2 });
    const NOMBRES = { HOTELES: 'Hoteles', OFERTAS: 'Solo Hotel', PAQUETES: 'Paquetes', CIRCUITOS: 'Circuitos', EXCURSIONES: 'Excursiones', TRASLADOS: 'Traslados', CRUCEROS: 'Cruceros' };
    const COSAS = { HOTELES: 'hotel', OFERTAS: 'oferta', PAQUETES: 'paquete', CIRCUITOS: 'salida', EXCURSIONES: 'excursión', TRASLADOS: 'traslado', CRUCEROS: 'crucero' };
    const cache = {};

    // ------------------------------------------------------------------ estilos
    const css = document.createElement('style');
    css.textContent = `
.pm-btn{display:inline-flex;align-items:center;gap:10px;border:0;cursor:pointer;font:inherit;border-radius:999px;padding:6px 8px 6px 6px;
  background:linear-gradient(135deg,#0B1215 0%,#18323A 100%);color:#fff;box-shadow:0 10px 24px -14px rgba(11,18,21,.7);transition:transform .18s,box-shadow .18s}
.pm-btn:hover{transform:translateY(-1px);box-shadow:0 14px 28px -14px rgba(11,18,21,.8)}
.pm-btn .pm-ico{width:30px;height:30px;border-radius:50%;display:grid;place-items:center;background:linear-gradient(135deg,#74F2E6,#2BD4C4);color:#0B1215;font-size:15px}
.pm-btn .pm-txt{display:flex;flex-direction:column;line-height:1.1;text-align:left}
.pm-btn .pm-txt small{font-size:10.5px;font-weight:700;letter-spacing:.06em;text-transform:uppercase;color:#9FE9E1}
.pm-btn .pm-txt b{font-size:13.5px;font-weight:800}
.pm-btn .pm-val{border-radius:999px;padding:5px 11px;font-size:12.5px;font-weight:800;background:rgba(255,255,255,.12);white-space:nowrap}
.pm-btn.on .pm-val{background:#74F2E6;color:#0B1215}
.pm-fondo{position:fixed;inset:0;z-index:4000;background:rgba(11,18,21,.5);backdrop-filter:blur(6px);display:grid;place-items:center;padding:16px;opacity:0;transition:opacity .22s}
.pm-fondo.on{opacity:1}
.pm-caja{position:relative;width:min(520px,100%);background:var(--ev-surface,#fff);color:var(--ev-text,#0B1215);border-radius:28px;overflow:hidden;font-family:var(--ev-font,inherit);
  box-shadow:0 30px 80px -24px rgba(11,18,21,.55);transform:translateY(14px) scale(.97);transition:transform .38s cubic-bezier(.34,1.56,.64,1)}
.pm-fondo.on .pm-caja{transform:none}
.pm-head{position:relative;padding:26px 28px 0;background:linear-gradient(160deg,#E8FBF8 0%,#F5FBFA 60%,var(--ev-surface,#fff) 100%);display:flex;gap:18px;align-items:center}
.pm-head img{width:118px;height:118px;object-fit:cover;border-radius:28px;background:#fff;box-shadow:0 0 0 6px #fff,0 16px 30px -16px rgba(11,18,21,.45);flex:none;animation:pmFlota 3.4s ease-in-out infinite}
.pm-head .t small{display:inline-block;font-size:11px;font-weight:800;letter-spacing:.14em;text-transform:uppercase;color:#0E8F86;background:#D9FBF6;border-radius:999px;padding:4px 10px}
.pm-head .t h3{margin:8px 0 4px;font-size:22px;font-weight:800;letter-spacing:-.02em;line-height:1.15}
.pm-head .t p{margin:0;color:var(--ev-muted,#66807C);font-weight:600;font-size:13.5px;line-height:1.45}
.pm-x{position:absolute;top:14px;right:14px;width:36px;height:36px;border-radius:12px;border:0;background:rgba(255,255,255,.8);cursor:pointer;font-size:16px;color:inherit}
.pm-body{padding:22px 28px 6px}
.pm-campo{display:flex;align-items:center;gap:10px;border:2px solid var(--ev-line,#E2EFED);border-radius:20px;padding:10px 16px;transition:border-color .15s,box-shadow .15s}
.pm-campo:focus-within{border-color:#2BD4C4;box-shadow:0 0 0 5px rgba(43,212,196,.18)}
.pm-campo span{font-weight:800;color:var(--ev-muted,#66807C);font-size:14px;white-space:nowrap}
.pm-campo input{flex:1;min-width:0;border:0;outline:0;background:transparent;font:800 34px var(--ev-font,inherit);color:inherit;letter-spacing:-.02em}
.pm-rap{display:flex;gap:8px;flex-wrap:wrap;margin-top:12px}
.pm-rap button{border:1px solid var(--ev-line,#E2EFED);background:var(--ev-surface,#fff);border-radius:999px;padding:6px 13px;font:700 12.5px var(--ev-font,inherit);cursor:pointer;color:inherit}
.pm-rap button:hover{border-color:#2BD4C4;background:#F0FDFB}
.pm-reglas{margin:16px 0 0;padding:0;list-style:none;display:grid;gap:8px}
.pm-reglas li{display:flex;gap:10px;align-items:flex-start;font-size:13px;font-weight:600;color:var(--ev-muted,#66807C);line-height:1.4}
.pm-reglas i{flex:none;width:24px;height:24px;border-radius:8px;display:grid;place-items:center;background:#F0FDFB;color:#0E8F86;font-size:13px}
.pm-pie{display:flex;gap:10px;align-items:center;padding:18px 28px 24px;flex-wrap:wrap}
.pm-pie .pm-quitar{margin-right:auto;border:0;background:none;color:#C9373C;font:700 13px var(--ev-font,inherit);cursor:pointer;display:inline-flex;gap:6px;align-items:center;padding:8px 4px}
.pm-b{border-radius:999px;padding:11px 20px;font:800 14px var(--ev-font,inherit);cursor:pointer;border:1px solid var(--ev-line,#E2EFED);background:var(--ev-surface,#fff);color:inherit;display:inline-flex;gap:8px;align-items:center}
.pm-b.pri{background:#0B1215;color:#fff;border-color:transparent}
.pm-b.pri:hover{background:#18323A}
.pm-b[disabled]{opacity:.5;cursor:wait}
.pm-err{margin-top:10px;color:#C9373C;font-weight:700;font-size:13px;min-height:18px}
.pm-av .pm-head{background:linear-gradient(160deg,#FFF4E5 0%,#FFFAF2 60%,var(--ev-surface,#fff) 100%)}
.pm-av .pm-head .t small{background:#FFE7C2;color:#9A5B00}
.pm-lista{margin:0;padding:12px 16px 12px 34px;background:#FFF7ED;border:1px solid #FED7AA;border-radius:16px;color:#7C2D12;font-weight:600;font-size:13px;line-height:1.5;max-height:200px;overflow:auto}
.pm-tip{display:flex;gap:10px;align-items:center;margin-top:12px;background:#F0FDFB;border-radius:14px;padding:10px 14px;font-size:13px;font-weight:600;color:#0E6F68}
@keyframes pmFlota{50%{transform:translateY(-6px)}}
@media (max-width:520px){.pm-head{flex-direction:column;text-align:center;padding-top:30px}.pm-head img{width:96px;height:96px}.pm-campo input{font-size:28px}.pm-btn .pm-txt small{display:none}}
@media (prefers-reduced-motion:reduce){.pm-head img{animation:none}}`;
    document.head.appendChild(css);

    // ------------------------------------------------------------------ datos
    async function valor(modulo, fresco = false) {
        if (!fresco && modulo in cache) return cache[modulo];
        try {
            const r = await fetch('/PreciosMinimos/Valor?modulo=' + encodeURIComponent(modulo), { headers: { 'Accept': 'application/json' } });
            const d = await r.json();
            cache[modulo] = d.minimo ?? null;
        } catch { cache[modulo] = cache[modulo] ?? null; }
        return cache[modulo];
    }

    async function guardar(modulo, precio) {
        const r = await fetch('/PreciosMinimos/Guardar', { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() }, body: JSON.stringify({ Modulo: modulo, Precio: precio }) });
        let d = null; try { d = await r.json(); } catch { }
        if (!r.ok) throw new Error(d?.mensaje || 'No se pudo guardar el precio mínimo.');
        cache[modulo] = d.minimo ?? null;
        pintarBotones(modulo);
        document.dispatchEvent(new CustomEvent('ev:precio-minimo', { detail: { modulo, minimo: cache[modulo] } }));
        return d;
    }

    // ------------------------------------------------------------------ modal base
    function modal(html, clase = '') {
        const f = document.createElement('div');
        f.className = 'pm-fondo';
        f.innerHTML = `<div class="pm-caja ${clase}" role="dialog" aria-modal="true">${html}</div>`;
        document.body.appendChild(f);
        requestAnimationFrame(() => f.classList.add('on'));
        const cerrar = () => { f.classList.remove('on'); document.removeEventListener('keydown', tecla, true); setTimeout(() => f.remove(), 230); };
        const tecla = e => { if (e.key === 'Escape') { e.stopPropagation(); cerrar(); } };
        document.addEventListener('keydown', tecla, true);
        f.addEventListener('click', e => { if (e.target === f || e.target.closest('[data-pm-cerrar]')) cerrar(); });
        return { f, cerrar };
    }

    // ------------------------------------------------------------------ establecer el mínimo
    async function abrir(modulo) {
        const nombre = NOMBRES[modulo] || modulo, cosa = COSAS[modulo] || 'registro';
        const actual = await valor(modulo, true);
        const { f, cerrar } = modal(`
            <button type="button" class="pm-x" data-pm-cerrar aria-label="Cerrar"><i class="bi bi-x-lg"></i></button>
            <div class="pm-head"><img src="/img/ava/robot-escritorio.jpg" alt=""><div class="t"><small>${esc(nombre)}</small>
                <h3>Precio mínimo de carga</h3><p>Ningún ${esc(cosa)} se podrá cargar con un precio menor a este valor, ni por el formulario ni por Excel.</p></div></div>
            <div class="pm-body">
                <label class="pm-campo"><span>Mínimo</span><input type="number" min="0" step="0.01" inputmode="decimal" placeholder="Sin mínimo" value="${actual ?? ''}" data-pm-input></label>
                <div class="pm-rap">${[10, 25, 30, 50, 100, 300].map(v => `<button type="button" data-pm-rap="${v}">${cifra(v)}</button>`).join('')}</div>
                <ul class="pm-reglas">
                    <li><i class="bi bi-shield-check"></i><span>Si un precio es menor, te aviso y <b>no se carga</b>: puedes ajustarlo ahí mismo y volver a guardar.</span></li>
                    <li><i class="bi bi-infinity"></i><span>Déjalo <b>vacío</b> para no tener mínimo: se puede cargar cualquier precio.</span></li>
                    <li><i class="bi bi-currency-exchange"></i><span>Se compara con el precio tal como se escribe, en la moneda de la carga.</span></li>
                </ul>
                <div class="pm-err" data-pm-err></div>
            </div>
            <div class="pm-pie">
                ${actual != null ? '<button type="button" class="pm-quitar" data-pm-quitar><i class="bi bi-x-circle"></i> Quitar el mínimo</button>' : '<span style="margin-right:auto"></span>'}
                <button type="button" class="pm-b" data-pm-cerrar>Cancelar</button>
                <button type="button" class="pm-b pri" data-pm-guardar><i class="bi bi-check2"></i> Guardar</button>
            </div>`);
        const inp = f.querySelector('[data-pm-input]'), err = f.querySelector('[data-pm-err]');
        setTimeout(() => inp.focus(), 120);
        f.querySelectorAll('[data-pm-rap]').forEach(b => b.onclick = () => { inp.value = b.dataset.pmRap; inp.focus(); });
        const enviar = async precio => {
            const b = f.querySelector('[data-pm-guardar]'); b.disabled = true; err.textContent = '';
            try { const d = await guardar(modulo, precio); cerrar(); aviso(d.mensaje); }
            catch (e) { err.textContent = e.message; b.disabled = false; }
        };
        f.querySelector('[data-pm-guardar]').onclick = () => {
            const t = inp.value.trim();
            if (t === '') return enviar(null);
            const n = Number(t);
            if (!isFinite(n) || n < 0) { err.textContent = 'Escribe un número de 0 en adelante, o déjalo vacío.'; return; }
            enviar(n);
        };
        inp.addEventListener('keydown', e => { if (e.key === 'Enter') f.querySelector('[data-pm-guardar]').click(); });
        f.querySelector('[data-pm-quitar]')?.addEventListener('click', () => enviar(null));
    }

    /** Aviso corto abajo (usa EV.toast si la pantalla lo tiene). */
    function aviso(t) {
        if (window.EV?.toast) return EV.toast(t);
        const d = document.createElement('div');
        d.style.cssText = 'position:fixed;left:50%;bottom:24px;transform:translateX(-50%);z-index:4100;background:#0B1215;color:#fff;padding:12px 18px;border-radius:14px;font-weight:700;font-size:14px;box-shadow:0 14px 30px -12px rgba(0,0,0,.5);max-width:90vw';
        d.textContent = t; document.body.appendChild(d); setTimeout(() => d.remove(), 4200);
    }

    // ------------------------------------------------------------------ aviso de precio bajo
    /** Muestra el aviso: «no se puede cargar por debajo de X» con la lista de lo que está bajo. Devuelve una promesa al cerrar. */
    function alerta({ modulo, minimo, lista = [], mensaje = '' }) {
        const nombre = NOMBRES[modulo] || modulo;
        return new Promise(ok => {
            const items = (lista.length ? lista : [mensaje]).filter(Boolean);
            const { f, cerrar } = modal(`
                <button type="button" class="pm-x" data-pm-cerrar aria-label="Cerrar"><i class="bi bi-x-lg"></i></button>
                <div class="pm-head"><img src="/img/ava/robot-saludo.jpg" alt=""><div class="t"><small>Precio mínimo · ${esc(nombre)}</small>
                    <h3>Hay precios por debajo de ${esc(cifra(minimo))}</h3><p>En ${esc(nombre)} no se puede cargar nada por debajo del mínimo. Ajusta el precio y vuelve a guardar.</p></div></div>
                <div class="pm-body">
                    ${items.length ? `<ul class="pm-lista">${items.slice(0, 30).map(x => `<li>${esc(x)}</li>`).join('')}${items.length > 30 ? `<li>… y ${items.length - 30} más</li>` : ''}</ul>` : ''}
                    <div class="pm-tip"><i class="bi bi-pencil-square"></i><span>Lo que escribiste sigue ahí: solo cambia el precio y guarda de nuevo.</span></div>
                </div>
                <div class="pm-pie"><button type="button" class="pm-quitar" style="color:#0E8F86" data-pm-cambiar><i class="bi bi-sliders"></i> Cambiar el mínimo</button>
                    <button type="button" class="pm-b pri" data-pm-cerrar><i class="bi bi-pencil"></i> Ajustar el precio</button></div>`, 'pm-av');
            f.querySelector('[data-pm-cambiar]').onclick = () => { cerrar(); abrir(modulo); };
            const obs = new MutationObserver(() => { if (!f.isConnected) { obs.disconnect(); ok(); } });
            obs.observe(document.body, { childList: true });
        });
    }

    /** Revisa precios antes de mandar: [{ precio, que }]. true = todo bien; false = mostró el aviso. */
    async function revisar(modulo, precios) {
        const min = await valor(modulo);
        if (!(min > 0)) return true;
        const bajos = precios.filter(p => Number(p.precio) > 0 && Number(p.precio) < min);
        if (!bajos.length) return true;
        await alerta({ modulo, minimo: min, lista: bajos.map(p => `${p.que ? p.que + ': ' : ''}${cifra(Number(p.precio))} (mínimo ${cifra(min)})`) });
        return false;
    }

    // ------------------------------------------------------------------ botones de las vistas
    function pintarBotones(soloModulo) {
        document.querySelectorAll('[data-precio-minimo]').forEach(async el => {
            const modulo = el.dataset.precioMinimo;
            if (soloModulo && modulo !== soloModulo) return;
            const v = await valor(modulo);
            el.innerHTML = `<button type="button" class="pm-btn ${v != null ? 'on' : ''}" title="Precio mínimo con que se puede cargar en ${esc(NOMBRES[modulo] || modulo)}">
                <span class="pm-ico"><i class="bi bi-shield-lock"></i></span><span class="pm-txt"><small>Precio mínimo</small><b>${v != null ? 'Activo' : 'Establecer'}</b></span>
                <span class="pm-val">${v != null ? '≥ ' + esc(cifra(v)) : 'Sin mínimo'}</span></button>`;
            el.querySelector('button').onclick = () => abrir(modulo);
        });
    }

    // ------------------------------------------------------------------ respuestas del servidor con «precioMinimo»
    // Formularios y Excel: el servidor agrega { precioMinimo: { modulo, minimo }, precioMinimoLista: [...] } cuando rechaza un precio.
    const original = window.fetch.bind(window);
    window.fetch = async (...args) => {
        const r = await original(...args);
        try {
            const metodo = (args[1]?.method || (args[0] instanceof Request ? args[0].method : 'GET')).toUpperCase();
            if (metodo !== 'GET' && (r.headers.get('content-type') || '').includes('json')) {
                r.clone().json().then(d => {
                    if (d?.precioMinimo?.minimo > 0) alerta({ modulo: d.precioMinimo.modulo, minimo: d.precioMinimo.minimo, lista: d.precioMinimoLista || [], mensaje: d.mensaje });
                }).catch(() => { });
            }
        } catch { }
        return r;
    };

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => pintarBotones());
    else pintarBotones();

    return { valor, abrir, alerta, revisar, pintarBotones, cifra };
})();
