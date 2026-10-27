// =====================================================================
// Barra de progreso de las cargas (Paquetes, Circuitos, Hoteles).
// Se carga en todas las páginas (_Layout) y no depende de eurovip-ui.js.
//
//   EVProgreso.abrir('Cargando paquetes')      -> { poner(pct, paso), cerrar() }
//   EVProgreso.conServidor(url, dto, { titulo }) -> POST que el servidor va reportando:
//        manda la cabecera X-Progreso y consulta {url-base}/Progreso?id=… cada medio segundo.
//   EVProgreso.subir(url, formData, alAvanzar)  -> subida de archivo con % real (XMLHttpRequest).
//
// Muestra porcentaje, paso actual y tiempo transcurrido; si pasan más de 20 s sin avances,
// lo dice ("sigue trabajando…") para que se sepa que no se colgó.
// =====================================================================
(function () {
    const css = `
.evp-fondo { position: fixed; inset: 0; z-index: 400; background: rgba(11,18,21,.45); display: grid; place-items: center; padding: 16px; opacity: 0; transition: opacity .2s; }
.evp-fondo.on { opacity: 1; }
.evp-caja { width: min(480px, 100%); background: var(--ev-surface, #fff); color: var(--ev-text, #0B1215); border-radius: 24px; padding: 26px 28px;
            box-shadow: 0 24px 60px rgba(11,18,21,.25); font-family: var(--ev-font, system-ui, sans-serif); }
.evp-ava { width: 108px; height: 108px; margin: -4px auto 14px; border-radius: 50%; overflow: hidden; background: #EAF6FF;
            box-shadow: 0 0 0 6px var(--ev-mint, #D9FBF6), 0 16px 32px -14px rgba(11,18,21,.35); animation: evp-flota 3.4s ease-in-out infinite; }
.evp-ava img { width: 100%; height: 100%; object-fit: cover; object-position: 50% 14%; display: block; }
@keyframes evp-flota { 50% { transform: translateY(-7px); } }
.evp-caja { text-align: center; }
.evp-pie { text-align: left; }
.evp-caja h3 { justify-content: center; }
.evp-caja h3 { margin: 0 0 4px; font-size: 18px; font-weight: 800; display: flex; align-items: center; gap: 10px; }
.evp-caja .evp-sub { margin: 0 0 18px; color: var(--ev-muted, #5B726F); font-size: 13px; font-weight: 600; }
.evp-pct { font-size: 40px; font-weight: 800; letter-spacing: -1px; line-height: 1; }
.evp-pct small { font-size: 16px; color: var(--ev-muted, #5B726F); }
.evp-barra { height: 14px; border-radius: 99px; background: var(--ev-surface-2, #EEF5F4); overflow: hidden; margin: 12px 0 10px; position: relative; }
.evp-barra i { display: block; height: 100%; width: 0; border-radius: 99px; background: linear-gradient(90deg, var(--ev-teal, #2BD4C4), var(--ev-aqua, #74F2E6));
               transition: width .4s ease; position: relative; overflow: hidden; }
.evp-barra i::after { content: ""; position: absolute; inset: 0; background: linear-gradient(90deg, transparent, rgba(255,255,255,.55), transparent);
                      transform: translateX(-100%); animation: evp-brillo 1.4s linear infinite; }
@keyframes evp-brillo { to { transform: translateX(100%); } }
.evp-paso { font-weight: 700; font-size: 14px; min-height: 20px; }
.evp-pie { display: flex; justify-content: space-between; gap: 10px; margin-top: 12px; color: var(--ev-muted, #5B726F); font-size: 12.5px; font-weight: 600; }
.evp-lento { color: #B54708; }
.evp-ok .evp-barra i { background: #12B76A; }
.evp-ok .evp-barra i::after { animation: none; }
@media (prefers-reduced-motion: reduce) { .evp-barra i, .evp-fondo { transition: none; } .evp-barra i::after, .evp-ava { animation: none; } }`;
    let estilo = null;

    const fmtTiempo = s => `${Math.floor(s / 60)}:${String(Math.floor(s % 60)).padStart(2, '0')}`;

    function abrir(titulo, sub = 'No cierres ni recargues esta página hasta que termine.') {
        if (!estilo) { estilo = document.createElement('style'); estilo.textContent = css; document.head.appendChild(estilo); }
        const f = document.createElement('div');
        f.className = 'evp-fondo';
        f.setAttribute('role', 'dialog'); f.setAttribute('aria-modal', 'true'); f.setAttribute('aria-label', titulo);
        f.innerHTML = `<div class="evp-caja"><div class="evp-ava"><img src="/img/ava/tablet.jpg" alt=""></div><h3></h3><p class="evp-sub"></p>
            <div class="evp-pct" aria-live="polite"><span>0</span><small>%</small></div>
            <div class="evp-barra" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow="0"><i></i></div>
            <div class="evp-paso">Empezando…</div>
            <div class="evp-pie"><span class="evp-tiempo">Tiempo: 0:00</span><span class="evp-estado"></span></div></div>`;
        f.querySelector('h3').textContent = titulo;
        f.querySelector('.evp-sub').textContent = sub;
        document.body.appendChild(f);
        requestAnimationFrame(() => f.classList.add('on'));
        const t0 = Date.now();
        let ultimo = Date.now(), pctActual = 0;
        const reloj = setInterval(() => {
            const s = (Date.now() - t0) / 1000, quieto = (Date.now() - ultimo) / 1000;
            f.querySelector('.evp-tiempo').textContent = 'Tiempo: ' + fmtTiempo(s);
            const e = f.querySelector('.evp-estado');
            if (quieto > 20 && pctActual < 100) { e.textContent = `Sigue trabajando… (sin avances hace ${Math.round(quieto)} s)`; e.className = 'evp-estado evp-lento'; }
            else { e.textContent = ''; e.className = 'evp-estado'; }
        }, 1000);
        const onBeforeUnload = ev => { ev.preventDefault(); ev.returnValue = ''; };
        window.addEventListener('beforeunload', onBeforeUnload);
        return {
            poner(pct, paso) {
                pct = Math.max(pctActual, Math.min(100, Math.round(pct || 0)));   // nunca retrocede
                if (pct !== pctActual || paso) ultimo = Date.now();
                pctActual = pct;
                f.querySelector('.evp-pct span').textContent = pct;
                f.querySelector('.evp-barra i').style.width = pct + '%';
                f.querySelector('.evp-barra').setAttribute('aria-valuenow', pct);
                if (paso) f.querySelector('.evp-paso').textContent = paso;
                if (pct >= 100) f.querySelector('.evp-caja').classList.add('evp-ok');
            },
            cerrar() {
                clearInterval(reloj);
                window.removeEventListener('beforeunload', onBeforeUnload);
                f.classList.remove('on');
                setTimeout(() => f.remove(), 220);
            }
        };
    }

    const idNuevo = () => (window.crypto?.randomUUID?.() || (Date.now().toString(36) + Math.random().toString(36).slice(2))).replace(/[^a-zA-Z0-9-]/g, '');
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const espera = ms => new Promise(r => setTimeout(r, ms));

    /** POST con progreso reportado por el servidor. Devuelve la respuesta JSON (o lanza Error con .data y .status). */
    async function conServidor(url, body, opts = {}) {
        const id = idNuevo();
        const base = opts.base || url.replace(/\/[^/]*$/, '');
        const ui = abrir(opts.titulo || 'Cargando…', opts.sub);
        ui.poner(2, 'Enviando los datos…');
        let fin = false;
        (async () => {
            while (!fin) {
                await espera(500);
                if (fin) break;
                try {
                    const r = await fetch(`${base}/Progreso?id=${encodeURIComponent(id)}`, { headers: { 'Accept': 'application/json' } });
                    if (r.ok) { const p = await r.json(); if (!fin && p) ui.poner(p.porcentaje, p.paso); }
                } catch { /* se reintenta en el siguiente ciclo */ }
            }
        })();
        try {
            const r = await fetch(url, {
                method: 'POST', body: JSON.stringify(body ?? {}),
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token(), 'X-Progreso': id }
            });
            let data = null;
            try { data = await r.json(); } catch { }
            fin = true;
            if (!r.ok) throw Object.assign(new Error(data?.mensaje || (r.status === 400 ? 'Revisa los datos.' : 'Algo salió mal. Intenta de nuevo.')), { data, status: r.status });
            ui.poner(100, '¡Listo!');
            await espera(600);
            return data;
        } finally {
            fin = true;
            ui.cerrar();
        }
    }

    /** Subida de un FormData con el % real de bytes enviados. alAvanzar(pct) opcional. */
    function subir(url, fd, alAvanzar) {
        return new Promise((ok, mal) => {
            const x = new XMLHttpRequest();
            x.open('POST', url);
            x.setRequestHeader('RequestVerificationToken', token());
            x.upload.onprogress = e => { if (e.lengthComputable && alAvanzar) alAvanzar(e.loaded / e.total * 100); };
            x.onload = () => {
                let data = null; try { data = JSON.parse(x.responseText); } catch { }
                if (x.status >= 200 && x.status < 300) ok(data);
                else mal(Object.assign(new Error(data?.mensaje || 'No se pudo subir el archivo.'), { data, status: x.status }));
            };
            x.onerror = () => mal(new Error('Se cortó la conexión al subir el archivo.'));
            x.send(fd);
        });
    }

    /** Subir un archivo mostrando la ventana de progreso (útil para el Excel de las cargas). */
    async function subirConVentana(url, fd, titulo, pasoFinal = 'Leyendo el archivo…') {
        const ui = abrir(titulo);
        try {
            const data = await subir(url, fd, p => p >= 100 ? ui.poner(75, pasoFinal) : ui.poner(p * 0.7, `Subiendo el archivo… ${Math.round(p)}%`));
            ui.poner(100, '¡Listo!');
            await espera(400);
            return data;
        } finally { ui.cerrar(); }
    }

    window.EVProgreso = { abrir, conServidor, subir, subirConVentana };
})();
