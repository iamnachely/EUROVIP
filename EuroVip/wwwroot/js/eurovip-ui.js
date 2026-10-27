// =====================================================================
// EUROVIP — utilidades comunes de interfaz (EV)
//   EV.api(url, opts)        fetch JSON con token anti-forgery y errores amigables
//   EV.toast(msg, tipo, acc) aviso flotante (con botón opcional)
//   EV.confirmar({...})      modal de confirmación (devuelve Promise<bool|string>)
//   EV.alerta({...})         modal amable de un botón (info / ok / aviso / error) con personaje
//   EV.esperar(titulo)       modal de espera animado con barra → { texto(t), cerrar() }
//                            (las acciones POST de EV.api lo muestran solas si tardan, y si fallan, la alerta)
//   EV.picker(input, cfg)    buscador con autocompletado (remoto o local)
//   EV.esc / fmt / fecha / debounce / tabs / confeti
//   EV.drawer(el)            panel lateral: avisa si se cierra con cambios sin guardar
//   EV.resaltar(el)          destaca un elemento recién guardado
//   EV.protegerSalida(fn)    avisa al salir de la página si fn() dice que hay trabajo sin guardar
//   EV.filtrosURL(st, def)   guarda/lee los filtros de una lista en la dirección (?q=…)
// =====================================================================
(function () {
    const $ = (s, el = document) => el.querySelector(s);

    const EV = {};
    EV.esc = s => (s ?? '').toString().replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    EV.debounce = (fn, ms = 250) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };
    EV.MONEDA = 'DOP';   // moneda del sistema: todo lo cargado está en pesos dominicanos
    EV.simbolo = (m = EV.MONEDA) => ({ EUR: '€', USD: 'US$', DOP: 'RD$' }[m] || m || 'RD$');
    EV.fmt = (n, dec = 0) => n == null || isNaN(n) ? '—' : Number(n).toLocaleString('es-ES', { minimumFractionDigits: dec, maximumFractionDigits: dec });
    EV.dinero = (n, m) => n == null || isNaN(n) ? '—' : `${EV.simbolo(m)} ${EV.fmt(n)}`;   // "RD$ 1.089"
    EV.iso = d => { const x = new Date(d); return `${x.getFullYear()}-${String(x.getMonth() + 1).padStart(2, '0')}-${String(x.getDate()).padStart(2, '0')}`; };
    EV.fecha = d => d ? new Date(typeof d === 'string' && d.length === 10 ? d + 'T00:00:00' : d).toLocaleDateString('es-ES', { day: '2-digit', month: 'short', year: 'numeric' }) : '—';
    EV.fechaCorta = d => new Date(typeof d === 'string' && d.length === 10 ? d + 'T00:00:00' : d).toLocaleDateString('es-ES', { weekday: 'short', day: '2-digit', month: 'short' });
    EV.sumarDias = (d, n) => { const x = new Date(typeof d === 'string' ? d + 'T00:00:00' : d); x.setDate(x.getDate() + n); return EV.iso(x); };
    EV.hoy = () => EV.iso(new Date());
    EV.iniciales = t => (t || '?').split(/\s+/).filter(Boolean).slice(0, 2).map(w => w[0].toUpperCase()).join('');
    EV.esUrl = s => /^https?:\/\//i.test(s || '');

    EV.token = () => $('input[name="__RequestVerificationToken"]')?.value || '';

    // ---------------- personajes (wwwroot/img/ava) y estilos de los modales amables ----------------
    // Cada modal lleva un personaje según lo que pasa: confirmar = lista, borrar/error = duda, éxito = equipo,
    // información = idea, ayuda/vacío = libro, esperando = trabajando / tablet.
    EV.AVA = {
        confirmar: ['/img/ava/lista.jpg', '50% 50%'], duda: ['/img/ava/duda.jpg', '50% 28%'], ok: ['/img/ava/equipo.jpg', '50% 50%'],
        info: ['/img/ava/idea.jpg', '45% 40%'], ayuda: ['/img/ava/libro.jpg', '50% 50%'],
        trabajando: ['/img/ava/trabajando.jpg', '50% 45%'], tablet: ['/img/ava/tablet.jpg', '50% 14%']
    };
    /** HTML del personaje redondo: clave de EV.AVA, icono pequeño opcional (bootstrap-icons) y tono (aqua|suave|ok|oro). */
    EV.avatar = (clave, { icono = '', tono = 'aqua', anim = 'pop' } = {}) => {
        const [src, pos] = EV.AVA[clave] || EV.AVA.info;
        return `<div class="ui-ava ${tono} ${anim}" aria-hidden="true"><div class="fr"><img src="${src}" alt="" style="object-position:${pos}"></div>${icono ? `<span class="badge"><i class="bi bi-${EV.esc(icono)}"></i></span>` : ''}</div>`;
    };
    const estilos = document.createElement('style');
    estilos.textContent = `
.ui-ava { position: relative; width: 124px; height: 124px; margin: -4px auto 16px; }
.ui-ava .fr { width: 100%; height: 100%; border-radius: 50%; overflow: hidden; background: #fff;
    box-shadow: 0 0 0 6px var(--ev-mint, #D9FBF6), 0 16px 32px -14px rgba(11,18,21,.35); }
.ui-ava img { width: 100%; height: 100%; object-fit: cover; display: block; }
.ui-ava.suave .fr { box-shadow: 0 0 0 6px #FFF1E0, 0 16px 32px -14px rgba(180,90,20,.35); }
.ui-ava.ok .fr { box-shadow: 0 0 0 6px #DCFCE7, 0 16px 32px -14px rgba(18,183,106,.35); }
.ui-ava.oro .fr { box-shadow: 0 0 0 6px #FFF1C7, 0 16px 32px -14px rgba(245,166,35,.4); }
.ui-ava .badge { position: absolute; right: 0; bottom: 4px; width: 40px; height: 40px; border-radius: 14px; display: grid; place-items: center; font-size: 18px;
    background: var(--ev-ink, #0B1215); color: var(--ev-aqua, #74F2E6); border: 3px solid var(--ev-surface, #fff); box-shadow: 0 6px 14px -4px rgba(0,0,0,.3); }
.ui-ava.suave .badge { background: #F79009; color: #fff; }
.ui-ava.ok .badge { background: #12B76A; color: #fff; }
.ui-ava.pop { animation: uiAvaPop .55s cubic-bezier(.34,1.56,.64,1) both; }
.ui-ava.flota { animation: uiAvaPop .55s cubic-bezier(.34,1.56,.64,1) both, uiAvaFlota 3.4s 0.6s ease-in-out infinite; }
@keyframes uiAvaPop { from { transform: scale(.55); opacity: 0; } }
@keyframes uiAvaFlota { 50% { transform: translateY(-7px); } }
.ui-modal .box { text-align: center; }
.ui-modal .box > p { line-height: 1.55; }
.ui-modal .lst { text-align: left; margin: 0 0 18px; padding: 12px 16px 12px 32px; background: #FFF7ED; border: 1px solid #FED7AA; border-radius: 16px;
    color: #7C2D12; font-weight: 600; font-size: 13.5px; line-height: 1.5; max-height: 220px; overflow: auto; }
/* espera amable */
.ui-esp-fondo { position: fixed; inset: 0; z-index: 390; background: rgba(11,18,21,.35); backdrop-filter: blur(4px); display: grid; place-items: center; padding: 16px; opacity: 0; transition: opacity .25s; }
.ui-esp-fondo.on { opacity: 1; }
.ui-esp { width: min(400px, 100%); background: var(--ev-surface, #fff); border-radius: 28px; padding: 30px 30px 22px; text-align: center; font-family: var(--ev-font, system-ui);
    box-shadow: 0 24px 60px rgba(11,18,21,.25); transform: translateY(12px) scale(.97); transition: transform .35s cubic-bezier(.34,1.56,.64,1); }
.ui-esp-fondo.on .ui-esp { transform: none; }
.ui-esp h3 { margin: 0 0 4px; font-weight: 800; font-size: 19px; letter-spacing: -.3px; }
.ui-esp p { margin: 0 0 18px; color: var(--ev-muted, #5B726F); font-weight: 600; font-size: 13.5px; min-height: 20px; transition: opacity .3s; }
.ui-esp-barra { height: 10px; border-radius: 99px; background: var(--ev-surface-2, #EEF5F4); overflow: hidden; position: relative; }
.ui-esp-barra i { position: absolute; top: 0; bottom: 0; width: 38%; border-radius: 99px; background: linear-gradient(90deg, var(--ev-teal, #2BD4C4), var(--ev-aqua, #74F2E6));
    animation: uiEspBarra 1.35s ease-in-out infinite; }
@keyframes uiEspBarra { 0% { left: -38%; } 100% { left: 100%; } }
.ui-esp-pie { display: flex; justify-content: space-between; margin-top: 10px; color: var(--ev-muted, #5B726F); font-size: 12px; font-weight: 700; }
/* pantallas vacías: el personaje del libro en vez del icono */
.ui-empty:has(> i.big)::before { content: ''; display: block; width: 116px; height: 116px; margin: 0 auto 10px; border-radius: 50%;
    background: #fff url(/img/ava/libro.jpg) center / 86% no-repeat; box-shadow: 0 0 0 6px var(--ev-mint, #D9FBF6); animation: uiAvaFlota 3.4s ease-in-out infinite; }
.ui-empty:has(> i.big) > i.big { display: none; }
@media (prefers-reduced-motion: reduce) { .ui-ava, .ui-ava.flota, .ui-esp-barra i, .ui-empty::before { animation: none !important; } .ui-esp-barra i { left: 0; width: 100%; opacity: .6; } }`;
    document.head.appendChild(estilos);

    // ---------------- espera amable (modal animado con barra) ----------------
    // EV.esperar('Duplicando…', ['Copiando fechas…', …]) → { texto(t), cerrar() }. Varias esperas a la vez comparten la ventana.
    let espera = null;
    EV.esperar = (titulo = 'Un momento, por favor…', frases) => {
        if (espera) { espera.n++; espera.titulo(titulo); return espera.api; }
        const lista = frases?.length ? frases : ['Estamos en ello, no tardamos…', 'Guardando todo con cuidado…', 'Ya casi está…', 'Gracias por la paciencia 💙'];
        const f = document.createElement('div');
        f.className = 'ui-esp-fondo';
        f.setAttribute('role', 'alertdialog'); f.setAttribute('aria-modal', 'true'); f.setAttribute('aria-busy', 'true');
        f.innerHTML = `<div class="ui-esp">${EV.avatar(Math.random() < .5 ? 'trabajando' : 'tablet', { anim: 'flota' })}
            <h3></h3><p aria-live="polite"></p><div class="ui-esp-barra"><i></i></div>
            <div class="ui-esp-pie"><span>No cierres esta página</span><span class="t">0 s</span></div></div>`;
        const h3 = f.querySelector('h3'), p = f.querySelector('p');
        h3.textContent = titulo; p.textContent = lista[0];
        document.body.appendChild(f);
        requestAnimationFrame(() => f.classList.add('on'));
        const t0 = Date.now();
        let i = 0;
        const reloj = setInterval(() => {
            const s = Math.round((Date.now() - t0) / 1000);
            f.querySelector('.t').textContent = s < 60 ? `${s} s` : `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')} min`;
            if (s && s % 3 === 0) { i = (i + 1) % lista.length; p.textContent = lista[i]; }
        }, 1000);
        espera = {
            n: 1, titulo: t => { h3.textContent = t; },
            api: {
                texto: t => { p.textContent = t; },
                cerrar: () => {
                    if (!espera || --espera.n > 0) return;
                    espera = null; clearInterval(reloj);
                    f.classList.remove('on'); setTimeout(() => f.remove(), 260);
                }
            }
        };
        return espera.api;
    };

    // ---------------- alerta amable (un botón) ----------------
    // EV.alerta({ titulo, texto, tipo: 'info'|'ok'|'aviso'|'error', lista: [..] | {campo: msg}, boton }) → Promise
    EV.alerta = ({ titulo, texto = '', tipo = 'info', lista = null, boton = 'Entendido' } = {}) => new Promise(resolve => {
        const cfg = {
            ok: ['ok', 'ok', 'check-lg', '¡Listo!'], info: ['info', 'aqua', 'lightbulb', 'Para que lo sepas'],
            aviso: ['ayuda', 'oro', 'exclamation-lg', 'Un momento'], error: ['duda', 'suave', 'arrow-repeat', 'Ups, algo no salió']
        }[tipo] || ['info', 'aqua', 'info-lg', ''];
        const items = !lista ? [] : Array.isArray(lista) ? lista : Object.values(lista).flat();
        const m = document.createElement('div');
        m.className = 'ui-modal';
        m.innerHTML = `<div class="box" role="alertdialog" aria-modal="true">${EV.avatar(cfg[0], { tono: cfg[1], icono: cfg[2] })}
            <h3></h3><p></p>${items.length ? `<ul class="lst">${items.map(x => `<li>${EV.esc(x)}</li>`).join('')}</ul>` : ''}
            <div class="acts"><button class="ev-btn" data-si>${EV.esc(boton)}</button></div></div>`;
        m.querySelector('h3').textContent = titulo || cfg[3];
        m.querySelector('p').innerHTML = texto;
        const bd = document.createElement('div'); bd.className = 'ui-backdrop'; bd.style.zIndex = 115;
        document.body.append(bd, m);
        requestAnimationFrame(() => { bd.classList.add('open'); m.classList.add('open'); });
        setTimeout(() => m.querySelector('[data-si]').focus(), 80);
        const cerrar = () => { m.classList.remove('open'); bd.classList.remove('open'); setTimeout(() => { m.remove(); bd.remove(); }, 300); document.removeEventListener('keydown', tecla); resolve(); };
        const tecla = e => { if (e.key === 'Escape' || e.key === 'Enter') { e.preventDefault(); cerrar(); } };
        document.addEventListener('keydown', tecla);
        m.querySelector('[data-si]').onclick = cerrar;
        bd.onclick = cerrar;
    });

    /** Qué está haciendo una acción, sacado de la dirección (/Cruceros/Duplicar → «Duplicando…»). */
    const queHace = url => {
        const a = ((url || '').split('?')[0].split('/').filter(Boolean).pop() || '').toLowerCase();
        const tabla = [[/^duplic/, 'Duplicando…'], [/^(elimin|borr|quit)/, 'Eliminando…'], [/^(subir|sub)/, 'Subiendo…'], [/^envi/, 'Enviando…'],
            [/^public/, 'Publicando…'], [/^(activ|desactiv|estado)/, 'Actualizando…'], [/^(orden|mover|reorden)/, 'Ordenando…'],
            [/^(valid|revis)/, 'Revisando…'], [/^(gener|crear)/, 'Creando…'], [/^(guard|edit|actualiz|cambi)/, 'Guardando…']];
        return (tabla.find(([r]) => r.test(a)) || [, 'Un momento, por favor…'])[1];
    };
    let ultimoError = { msg: '', t: 0 };

    // Las acciones (POST/PUT/DELETE) muestran sola la espera amable si tardan más de 350 ms, y si fallan una alerta
    // amable (el aviso flotante con el mismo mensaje que suele poner la pantalla ya no se repite).
    // opts.silencioso = sin espera ni alerta (guardados automáticos); opts.sinAlerta = solo sin la alerta.
    EV.api = async (url, opts = {}) => {
        const esForm = opts.body instanceof FormData;
        const accion = !!opts.method && opts.method.toUpperCase() !== 'GET' && !opts.silencioso;
        let esp = null, t0 = 0;
        const reloj = accion ? setTimeout(() => { esp = EV.esperar(opts.espera || queHace(url)); t0 = Date.now(); }, 350) : null;
        const cerrarEspera = async () => {
            clearTimeout(reloj);
            if (esp) { const falta = 500 - (Date.now() - t0); if (falta > 0) await new Promise(r => setTimeout(r, falta)); esp.cerrar(); }
        };
        let r, data = null;
        try {
            const { silencioso, sinAlerta, espera: _e, ...fetchOpts } = opts;
            r = await fetch(url, {
                ...fetchOpts,
                headers: { ...(esForm ? {} : { 'Content-Type': 'application/json' }), 'RequestVerificationToken': EV.token(), ...(opts.headers || {}) }
            });
            try { data = await r.json(); } catch { }
        } catch {
            await cerrarEspera();
            const msg = 'No pudimos hablar con el servidor. Revisa tu conexión e inténtalo de nuevo.';
            if (accion && !opts.sinAlerta) { ultimoError = { msg, t: Date.now() }; EV.alerta({ tipo: 'error', titulo: 'Se cortó la conexión', texto: EV.esc(msg) }); }
            throw Object.assign(new Error(msg), { status: 0 });
        }
        await cerrarEspera();
        if (!r.ok) {
            const msg = data?.mensaje || (r.status === 400 ? 'Revisa los datos.' : r.status === 404 ? 'No encontrado.' : 'Algo salió mal. Intenta de nuevo.');
            if (accion && !opts.sinAlerta) {
                ultimoError = { msg, t: Date.now() };
                EV.alerta({ tipo: r.status === 400 || r.status === 409 ? 'aviso' : 'error',
                    titulo: r.status === 400 || r.status === 409 ? 'Revisemos un detalle' : 'Ups, algo no salió',
                    texto: EV.esc(msg) + (r.status >= 500 ? '<br><small style="color:var(--ev-muted)">No es tu culpa: inténtalo de nuevo en un momento. Si se repite, avísale a sistemas.</small>' : ''),
                    lista: data?.errores && typeof data.errores === 'object' ? data.errores : null });
            }
            throw Object.assign(new Error(msg), { data, status: r.status });
        }
        // Un guardado que salió bien deja los paneles abiertos "sin cambios pendientes"
        if (opts.method && opts.method.toUpperCase() !== 'GET') drawers.forEach(d => d.limpio());
        return data;
    };
    EV.post = (url, body) => EV.api(url, { method: 'POST', body: body instanceof FormData ? body : JSON.stringify(body ?? {}) });

    // ---------------- toasts ----------------
    EV.toast = (msg, tipo = 'ok', accion) => {
        // si ese error ya lo explicó la alerta amable hace un momento, no repetirlo abajo
        if (tipo === 'err' && msg === ultimoError.msg && Date.now() - ultimoError.t < 5000) return;
        let cont = $('.ui-toasts');
        if (!cont) {
            cont = document.createElement('div'); cont.className = 'ui-toasts';
            cont.setAttribute('role', 'status'); cont.setAttribute('aria-live', 'polite');   // lectores de pantalla
            document.body.appendChild(cont);
        }
        const t = document.createElement('div');
        t.className = 'ui-toast' + (tipo === 'err' ? ' err' : '');
        t.innerHTML = `<i class="bi bi-${tipo === 'err' ? 'exclamation-octagon' : tipo === 'info' ? 'info-circle' : 'check-circle-fill'}"></i><span></span>`;
        t.querySelector('span').textContent = msg;
        const quitar = () => { t.classList.add('out'); setTimeout(() => t.remove(), 300); };
        if (accion) {
            const b = document.createElement('button'); b.textContent = accion.texto;
            b.onclick = () => { accion.accion(); quitar(); };
            t.appendChild(b);
        }
        cont.appendChild(t);
        setTimeout(quitar, accion ? 7000 : tipo === 'err' ? 6000 : 3800);
    };

    // ---------------- confirmación ----------------
    EV.confirmar = ({ titulo, texto, si = 'Confirmar', no = 'Cancelar', peligro = false, icono, escribir }) => new Promise(resolve => {
        const m = document.createElement('div');
        m.className = 'ui-modal';
        m.innerHTML = `<div class="box" role="dialog" aria-modal="true">
            ${EV.avatar(peligro ? 'duda' : 'confirmar', { tono: peligro ? 'suave' : 'aqua', icono: icono || (peligro ? 'trash3' : 'question-lg') })}
            <h3></h3><p></p>
            ${escribir ? `<div class="ui-f"><label>Para confirmar escribe <b>${EV.esc(escribir)}</b></label><input class="ui-input" autocomplete="off"></div>` : ''}
            <div class="acts"><button class="ev-btn ${peligro ? 'red' : ''}" data-si>${EV.esc(si)}</button><button class="ev-btn ghost" data-no>${EV.esc(no)}</button></div></div>`;
        m.querySelector('h3').textContent = titulo;
        m.querySelector('p').innerHTML = texto;
        const bd = document.createElement('div'); bd.className = 'ui-backdrop'; bd.style.zIndex = 115;
        document.body.append(bd, m);
        requestAnimationFrame(() => { bd.classList.add('open'); m.classList.add('open'); });
        const btnSi = m.querySelector('[data-si]');
        const inp = m.querySelector('input');
        if (inp) { btnSi.disabled = true; inp.addEventListener('input', () => btnSi.disabled = inp.value.trim().toUpperCase() !== escribir.toUpperCase()); setTimeout(() => inp.focus(), 80); }
        else setTimeout(() => m.querySelector('[data-no]').focus(), 80);
        const cerrar = v => { m.classList.remove('open'); bd.classList.remove('open'); setTimeout(() => { m.remove(); bd.remove(); }, 300); resolve(v); document.removeEventListener('keydown', esc); };
        const esc = e => { if (e.key === 'Escape') cerrar(false); };
        document.addEventListener('keydown', esc);
        btnSi.onclick = () => cerrar(true);
        m.querySelector('[data-no]').onclick = () => cerrar(false);
        bd.onclick = () => cerrar(false);
    });

    // ---------------- pedir un dato (reemplaza al prompt() del navegador) ----------------
    // EV.pedir({ titulo, texto, valor, etiqueta, si, tipo: 'text'|'number', min, max, maxlength, ayuda })
    // → Promise<string|null> (null = cancelado). Enter acepta, Esc cancela; no deja aceptar vacío.
    EV.pedir = ({ titulo, texto = '', valor = '', etiqueta = '', si = 'Guardar', no = 'Cancelar', tipo = 'text', min, max, maxlength = 200, ayuda = '', icono = 'pencil' }) => new Promise(resolve => {
        const m = document.createElement('div');
        m.className = 'ui-modal';
        m.innerHTML = `<div class="box" role="dialog" aria-modal="true">
            ${EV.avatar('info', { icono })}
            <h3></h3>${texto ? '<p></p>' : ''}
            <div class="ui-f" style="text-align:left">${etiqueta ? `<label>${EV.esc(etiqueta)}</label>` : ''}
                <input class="ui-input" type="${tipo === 'number' ? 'number' : 'text'}" autocomplete="off" maxlength="${maxlength}" ${min != null ? `min="${min}"` : ''} ${max != null ? `max="${max}"` : ''}>
                <span class="msg"></span>${ayuda ? `<span class="ui-help">${EV.esc(ayuda)}</span>` : ''}</div>
            <div class="acts"><button class="ev-btn" data-si>${EV.esc(si)}</button><button class="ev-btn ghost" data-no>${EV.esc(no)}</button></div></div>`;
        m.querySelector('h3').textContent = titulo;
        if (texto) m.querySelector('p').innerHTML = texto;
        const inp = m.querySelector('input'), msg = m.querySelector('.msg'), f = m.querySelector('.ui-f');
        inp.value = valor ?? '';
        const bd = document.createElement('div'); bd.className = 'ui-backdrop'; bd.style.zIndex = 115;
        document.body.append(bd, m);
        requestAnimationFrame(() => { bd.classList.add('open'); m.classList.add('open'); });
        setTimeout(() => { inp.focus(); inp.select(); }, 80);
        const cerrar = v => { m.classList.remove('open'); bd.classList.remove('open'); setTimeout(() => { m.remove(); bd.remove(); }, 300); resolve(v); document.removeEventListener('keydown', tecla); };
        const aceptar = () => {
            const v = inp.value.trim();
            let err = !v ? 'Escribe un valor.' : '';
            if (!err && tipo === 'number') {
                const n = Number(v);
                if (isNaN(n)) err = 'Escribe un número.';
                else if (min != null && n < min) err = `Mínimo ${min}.`;
                else if (max != null && n > max) err = `Máximo ${max}.`;
            }
            if (err) { f.classList.add('err'); msg.textContent = err; inp.focus(); return; }
            cerrar(v);
        };
        const tecla = e => { if (e.key === 'Escape') cerrar(null); else if (e.key === 'Enter' && document.activeElement === inp) { e.preventDefault(); aceptar(); } };
        document.addEventListener('keydown', tecla);
        inp.addEventListener('input', () => { f.classList.remove('err'); msg.textContent = ''; });
        m.querySelector('[data-si]').onclick = aceptar;
        m.querySelector('[data-no]').onclick = () => cerrar(null);
        bd.onclick = () => cerrar(null);
    });

    // ---------------- buscador con autocompletado ----------------
    // cfg: { url: q => '/x?q=' + q | null, datos: [] (local), map: item => ({valor, texto, sub, img, icono}),
    //        onSelect: (item, raw) => {}, min: 1, limpiar: true, placeholder }
    EV.picker = (input, cfg) => {
        const wrap = document.createElement('div');
        wrap.className = 'ui-picker-wrap';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);
        input.setAttribute('autocomplete', 'off');
        const lista = document.createElement('div');
        lista.className = 'ui-picker-list';
        lista.hidden = true;
        wrap.appendChild(lista);
        let items = [], act = -1, ultimo = null;
        const map = cfg.map || (x => ({ valor: x.valor ?? x.Valor, texto: x.texto ?? x.Texto, sub: x.extra ?? x.Extra }));

        const pintar = () => {
            if (!items.length) { lista.innerHTML = `<div class="vacio">${input.value.trim() ? 'Sin resultados' : 'Escribe para buscar…'}</div>`; return; }
            lista.innerHTML = items.map((it, i) => {
                const m = map(it);
                const ico = m.img ? `<span class="ico"><img src="${EV.esc(m.img)}" alt="" onerror="this.remove()"></span>` : m.icono ? `<span class="ico"><i class="bi ${m.icono}"></i></span>` : '';
                return `<button type="button" data-i="${i}" class="${i === act ? 'act' : ''}">${ico}<span>${EV.esc(m.texto)}</span>${m.sub ? `<small>${EV.esc(m.sub)}</small>` : ''}</button>`;
            }).join('');
        };

        const buscar = EV.debounce(async () => {
            const q = input.value.trim();
            if (cfg.datos) {
                const n = q.toLowerCase();
                const fuente = typeof cfg.datos === 'function' ? cfg.datos() : cfg.datos;
                items = fuente.filter(x => { const m = map(x); return !n || `${m.texto} ${m.sub || ''} ${m.valor}`.toLowerCase().includes(n); }).slice(0, 60);
            } else {
                if (q.length < (cfg.min ?? 1)) { items = []; pintar(); return; }
                const url = cfg.url(encodeURIComponent(q));
                ultimo = url;
                try { const r = await EV.api(url); if (ultimo === url) items = r; } catch { items = []; }
            }
            act = items.length ? 0 : -1;
            pintar();
        }, cfg.datos ? 60 : 220);

        const elegir = i => {
            const raw = items[i]; if (!raw) return;
            const m = map(raw);
            lista.hidden = true;
            if (cfg.limpiar !== false) input.value = ''; else input.value = m.texto;
            cfg.onSelect?.(m, raw);
        };

        input.addEventListener('focus', () => { lista.hidden = false; buscar(); });
        input.addEventListener('input', () => { lista.hidden = false; buscar(); });
        input.addEventListener('keydown', e => {
            if (lista.hidden) return;
            if (e.key === 'ArrowDown') { e.preventDefault(); act = Math.min(act + 1, items.length - 1); pintar(); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); act = Math.max(act - 1, 0); pintar(); }
            else if (e.key === 'Enter') { e.preventDefault(); if (act >= 0) elegir(act); }
            // Esc solo cierra la lista de sugerencias: no debe llegar a la pantalla (que cerraría el panel entero)
            else if (e.key === 'Escape') { lista.hidden = true; e.stopPropagation(); }
        });
        lista.addEventListener('mousedown', e => { const b = e.target.closest('[data-i]'); if (b) { e.preventDefault(); elegir(+b.dataset.i); } });
        input.addEventListener('blur', () => setTimeout(() => lista.hidden = true, 150));
        return { refrescar: buscar, input };
    };

    // ---------------- pestañas ----------------
    EV.tabs = (barra, alCambiar) => {
        const botones = [...barra.querySelectorAll('[data-tab]')];
        const ir = id => {
            botones.forEach(b => b.classList.toggle('on', b.dataset.tab === id));
            botones.forEach(b => document.getElementById(b.dataset.tab)?.classList.toggle('on', b.dataset.tab === id));
            alCambiar?.(id);
        };
        botones.forEach(b => b.addEventListener('click', () => ir(b.dataset.tab)));
        return { ir };
    };

    // ---------------- confeti ----------------
    EV.confeti = () => {
        if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        const c = document.createElement('div'); c.className = 'ui-confeti';
        const colores = ['#B3FFF3', '#74F2E6', '#2BD4C4', '#0B1215', '#FFE08A', '#CE1126', '#002D62'];
        for (let i = 0; i < 90; i++) {
            const p = document.createElement('i');
            p.style.left = Math.random() * 100 + 'vw';
            p.style.background = colores[i % colores.length];
            p.style.animationDuration = 1.8 + Math.random() * 2 + 's';
            p.style.animationDelay = Math.random() * .6 + 's';
            p.style.transform = `rotate(${Math.random() * 360}deg)`;
            c.appendChild(p);
        }
        document.body.appendChild(c);
        setTimeout(() => c.remove(), 4800);
    };

    // ---------------- drawer ----------------
    // Panel lateral. Si el usuario escribió o cambió algo dentro y lo intenta cerrar (X, Esc, clic fuera,
    // Cancelar) sin guardar, pregunta antes de perder los cambios. Un guardado correcto (EV.post) lo deja limpio.
    const drawers = [];
    EV.drawer = (el) => {
        let bd = $('.ui-backdrop[data-drawer]');
        if (!bd) { bd = document.createElement('div'); bd.className = 'ui-backdrop'; bd.dataset.drawer = '1'; document.body.appendChild(bd); }
        let sucio = false, preguntando = false;
        const marcar = e => { if (e.isTrusted && el.classList.contains('open')) sucio = true; };
        el.addEventListener('input', marcar);
        el.addEventListener('change', marcar);
        // Cambios hechos por código que el usuario sí pidió (p. ej. subir o quitar una imagen): disparar 'ev-cambio'
        el.addEventListener('ev-cambio', () => { if (el.classList.contains('open')) sucio = true; });
        const cerrarYa = () => { sucio = false; bd.classList.remove('open'); el.classList.remove('open'); document.body.style.overflow = ''; };
        const api = {
            abrir() { sucio = false; bd.classList.add('open'); el.classList.add('open'); document.body.style.overflow = 'hidden'; },
            /** cerrar(true) cierra sin preguntar (por ejemplo después de guardar). */
            async cerrar(forzar) {
                if (!el.classList.contains('open')) return;
                // Si hay un cuadro de confirmación abierto, el Esc es para ese cuadro, no para el panel
                if (!forzar && $('.ui-modal.open')) return;
                if (sucio && !forzar) {
                    if (preguntando) return;
                    preguntando = true;
                    const ok = await EV.confirmar({ titulo: '¿Cerrar sin guardar?', icono: 'exclamation-triangle', peligro: true,
                        texto: 'Hiciste cambios en este panel que <b>todavía no se guardaron</b>. Si cierras, se pierden.', si: 'Cerrar sin guardar', no: 'Seguir editando' });
                    preguntando = false;
                    if (!ok) return;
                }
                cerrarYa();
            },
            abierto: () => el.classList.contains('open'),
            sucio: () => sucio && el.classList.contains('open'),
            limpio() { sucio = false; }
        };
        bd.addEventListener('click', () => { if (el.classList.contains('open')) api.cerrar(); });
        drawers.push(api);
        return api;
    };
    // Avisar también si se sale de la página (menú, recargar, cerrar pestaña) con un panel a medio editar.
    window.addEventListener('beforeunload', e => { if (drawers.some(d => d.sucio())) { e.preventDefault(); e.returnValue = ''; } });

    // ---------------- resaltar lo recién guardado ----------------
    EV.resaltar = el => {
        if (!el) return;
        el.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        el.animate([{ boxShadow: '0 0 0 0 rgba(116,242,230,.95)' }, { boxShadow: '0 0 0 14px rgba(116,242,230,0)' }], { duration: 1100, iterations: 2 });
    };

    // ---------------- proteger una pantalla larga (asistentes de carga) ----------------
    // fn() devuelve true si hay trabajo sin guardar. Al terminar bien, llamar al .liberar() devuelto.
    EV.protegerSalida = fn => {
        let activo = true;
        const h = e => { if (activo && fn()) { e.preventDefault(); e.returnValue = ''; } };
        window.addEventListener('beforeunload', h);
        return { liberar() { activo = false; }, activar() { activo = true; } };
    };

    // ---------------- filtros en la dirección (para volver y encontrarlos igual) ----------------
    // EV.filtrosURL.leer(def) → objeto con los valores de ?clave=… (solo las claves de def, con su tipo)
    // EV.filtrosURL.guardar(st, def) → escribe en la dirección solo lo que no es el valor por defecto
    EV.filtrosURL = {
        leer(def) {
            const p = new URLSearchParams(location.search), r = {};
            for (const [k, v0] of Object.entries(def)) {
                if (!p.has(k)) continue;
                const v = p.get(k);
                r[k] = typeof v0 === 'number' ? (Number(v) || v0) : typeof v0 === 'boolean' ? v === '1' : v;
            }
            return r;
        },
        guardar(st, def) {
            const p = new URLSearchParams(location.search);
            for (const [k, v0] of Object.entries(def)) {
                const v = st[k];
                if (v === v0 || v === '' || v == null || v === false) p.delete(k);
                else p.set(k, typeof v === 'boolean' ? '1' : v);
            }
            const q = p.toString();
            history.replaceState(null, '', location.pathname + (q ? '?' + q : '') + location.hash);
        }
    };

    // ---------------- "/" para buscar en cualquier pantalla ----------------
    document.addEventListener('keydown', e => {
        if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) return;
        const a = document.activeElement;
        if (a && (/^(input|textarea|select)$/i.test(a.tagName) || a.isContentEditable)) return;
        if ($('.ui-drawer.open, .hx-drawer.open, .ui-modal.open')) return;
        const s = $('main .ui-search input, main input[type=search], .ui-search input');
        if (s) { e.preventDefault(); s.focus(); s.select?.(); }
    });

    // ---------------- formularios ligeros: lo opcional y vacío, recogido en «Más opciones» ----------------
    // Un campo es opcional si su etiqueta dice «opcional» (<em>…opcional…</em>) o lleva data-opcional. Si está vacío
    // se esconde y la sección muestra un botón «Más opciones (n): Nombre, Prioridad…». Lo que tiene dato, lo que tiene
    // error y lo que el usuario ya abrió se ve siempre. data-siempre en un campo (o en su sección) lo deja fuera.
    const CAMPO = '.ui-f, .ev-prio-campos > div';
    const esOpcional = f => f.hasAttribute('data-opcional') || /opcional/i.test(f.querySelector(':scope > label em')?.textContent || '');
    const tieneDato = f => [...f.querySelectorAll('input, select, textarea')].some(x =>
        x.type === 'checkbox' || x.type === 'radio' ? x.checked : x.tagName === 'SELECT' ? x.selectedIndex > 0 && x.value !== '' : x.value.trim() !== '');
    const nombreCampo = f => {
        const l = f.querySelector(':scope > label')?.cloneNode(true);
        l?.querySelectorAll('em, small').forEach(x => x.remove());
        return (l?.textContent || 'Campo').replace(/[*:]/g, '').replace(/\s+/g, ' ').trim();
    };
    EV.plegar = (raiz = document) => {
        const cands = [...raiz.querySelectorAll(CAMPO)].filter(f => esOpcional(f) && !f.closest('.ui-modal, [data-siempre], [data-abierto]'));
        // primero mostrar lo que ya no debe estar escondido (se le cargó un dato o tiene error)
        cands.filter(f => f.classList.contains('ui-plegado') && (tieneDato(f) || f.classList.contains('err'))).forEach(f => f.classList.remove('ui-plegado'));
        const grupos = new Map();
        cands.filter(f => !tieneDato(f) && !f.classList.contains('err')).forEach(f => {
            const g = f.closest('.ui-seg, .ui-sec, [data-grupo], [data-paso], .ui-pane, .ui-db, .hx-db, .xp-panel, form') || f.parentElement;
            if (!grupos.has(g)) grupos.set(g, []);
            grupos.get(g).push(f);
        });
        grupos.forEach((fs, g) => {
            fs.forEach(f => f.classList.add('ui-plegado'));
            // filas o bloques que se quedaron sin nada visible, también fuera
            new Set(fs.map(f => f.parentElement)).forEach(p => {
                if (p !== g && [...p.children].every(h => h.classList.contains('ui-plegado') || !h.matches(CAMPO))) p.classList.add('ui-plegado');
            });
            let b = [...g.children].find(x => x.classList?.contains('ui-mas'));
            const nombres = fs.map(nombreCampo);
            if (!b) {
                b = document.createElement('button');
                b.type = 'button'; b.className = 'ui-mas';
                b.onclick = () => {
                    g.setAttribute('data-abierto', '');
                    g.querySelectorAll('.ui-plegado').forEach(x => { x.classList.remove('ui-plegado'); x.classList.add('ui-desplegado'); });
                    b.remove();
                    g.querySelector('.ui-desplegado input:not([type=hidden]), .ui-desplegado select, .ui-desplegado textarea')?.focus();
                };
                let ult = fs[fs.length - 1];
                while (ult.parentElement && ult.parentElement !== g) ult = ult.parentElement;
                ult.after(b);
            }
            const html = `<i class="bi bi-plus-circle"></i><span>Más opciones <b>(${fs.length})</b><small>${EV.esc(nombres.slice(0, 4).join(' · '))}${nombres.length > 4 ? '…' : ''}</small></span><em>no son obligatorias</em>`;
            if (b.dataset.h !== html) { b.dataset.h = html; b.innerHTML = html; }   // sin cambios = sin mutaciones (el observador no se repite)
        });
        // botones que ya no esconden nada
        raiz.querySelectorAll('.ui-mas').forEach(b => { if (!b.parentElement.querySelector('.ui-plegado')) b.remove(); });
    };
    const estPlegar = document.createElement('style');
    estPlegar.textContent = `
.ui-plegado { display: none !important; }
.ui-desplegado { animation: uiDespliega .35s ease both; }
@keyframes uiDespliega { from { opacity: 0; transform: translateY(-6px); } }
.ui-mas { display: flex; align-items: center; gap: 12px; width: 100%; margin: 4px 0 16px; padding: 12px 16px; border: 1.5px dashed var(--ev-line, #DCE6E4); border-radius: 16px;
    background: var(--ev-surface-2, #F4F8F7); color: var(--ev-text, #0B1215); font: 700 13.5px var(--ev-font, system-ui); text-align: left; cursor: pointer; transition: all .2s; }
.ui-mas:hover { border-color: var(--ev-teal, #2BD4C4); background: var(--ev-mint, #E6FBF8); }
.ui-mas > i { font-size: 18px; color: var(--ev-teal, #2BD4C4); }
.ui-mas small { display: block; color: var(--ev-muted, #5B726F); font-weight: 600; font-size: 12px; margin-top: 2px; }
.ui-mas em { margin-left: auto; font-style: normal; font-size: 11.5px; font-weight: 700; color: var(--ev-muted, #5B726F); background: var(--ev-surface, #fff); padding: 4px 10px; border-radius: 99px; white-space: nowrap; }
@media (prefers-reduced-motion: reduce) { .ui-desplegado { animation: none; } }`;
    document.head.appendChild(estPlegar);
    // Se aplica solo: al abrir un panel (cuando ya tiene sus datos cargados) y cuando se pinta un formulario nuevo.
    // Se repasa otra vez al rato por si los datos del registro llegan después de pintar el formulario.
    let plegarPend = null, plegarTarde = null;
    const plegarLuego = () => {
        clearTimeout(plegarPend); clearTimeout(plegarTarde);
        plegarPend = setTimeout(() => EV.plegar(), 150); plegarTarde = setTimeout(() => EV.plegar(), 900);
    };
    const propia = m => m.type === 'childList' && [...m.addedNodes, ...m.removedNodes].every(n => n.classList?.contains('ui-mas') || m.target.classList?.contains('ui-mas'));
    const vigilarPlegar = () => { plegarLuego(); new MutationObserver(ms => { if (ms.some(m => (m.type === 'childList' && !propia(m)) || m.target.classList?.contains('open') || m.target.classList?.contains('err'))) plegarLuego(); })
        .observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] }); };
    if (document.body) vigilarPlegar(); else document.addEventListener('DOMContentLoaded', vigilarPlegar);

    window.EV = EV;
})();
