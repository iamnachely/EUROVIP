// =====================================================================
// EUROVIP — utilidades comunes de interfaz (EV)
//   EV.api(url, opts)        fetch JSON con token anti-forgery y errores amigables
//   EV.toast(msg, tipo, acc) aviso flotante (con botón opcional)
//   EV.confirmar({...})      modal de confirmación (devuelve Promise<bool|string>)
//   EV.picker(input, cfg)    buscador con autocompletado (remoto o local)
//   EV.esc / fmt / fecha / debounce / tabs / confeti
// =====================================================================
(function () {
    const $ = (s, el = document) => el.querySelector(s);

    const EV = {};
    EV.esc = s => (s ?? '').toString().replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    EV.debounce = (fn, ms = 250) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };
    EV.simbolo = m => ({ EUR: '€', USD: 'US$', DOP: 'RD$' }[m] || m || '€');
    EV.fmt = (n, dec = 0) => n == null || isNaN(n) ? '—' : Number(n).toLocaleString('es-ES', { minimumFractionDigits: dec, maximumFractionDigits: dec });
    EV.dinero = (n, m) => n == null || isNaN(n) ? '—' : `${EV.fmt(n)} ${EV.simbolo(m)}`;
    EV.iso = d => { const x = new Date(d); return `${x.getFullYear()}-${String(x.getMonth() + 1).padStart(2, '0')}-${String(x.getDate()).padStart(2, '0')}`; };
    EV.fecha = d => d ? new Date(typeof d === 'string' && d.length === 10 ? d + 'T00:00:00' : d).toLocaleDateString('es-ES', { day: '2-digit', month: 'short', year: 'numeric' }) : '—';
    EV.fechaCorta = d => new Date(typeof d === 'string' && d.length === 10 ? d + 'T00:00:00' : d).toLocaleDateString('es-ES', { weekday: 'short', day: '2-digit', month: 'short' });
    EV.sumarDias = (d, n) => { const x = new Date(typeof d === 'string' ? d + 'T00:00:00' : d); x.setDate(x.getDate() + n); return EV.iso(x); };
    EV.hoy = () => EV.iso(new Date());
    EV.iniciales = t => (t || '?').split(/\s+/).filter(Boolean).slice(0, 2).map(w => w[0].toUpperCase()).join('');
    EV.esUrl = s => /^https?:\/\//i.test(s || '');

    EV.token = () => $('input[name="__RequestVerificationToken"]')?.value || '';

    EV.api = async (url, opts = {}) => {
        const esForm = opts.body instanceof FormData;
        const r = await fetch(url, {
            ...opts,
            headers: { ...(esForm ? {} : { 'Content-Type': 'application/json' }), 'RequestVerificationToken': EV.token(), ...(opts.headers || {}) }
        });
        let data = null;
        try { data = await r.json(); } catch { }
        if (!r.ok) {
            const msg = data?.mensaje || (r.status === 400 ? 'Revisa los datos.' : r.status === 404 ? 'No encontrado.' : 'Algo salió mal. Intenta de nuevo.');
            throw Object.assign(new Error(msg), { data, status: r.status });
        }
        return data;
    };
    EV.post = (url, body) => EV.api(url, { method: 'POST', body: body instanceof FormData ? body : JSON.stringify(body ?? {}) });

    // ---------------- toasts ----------------
    EV.toast = (msg, tipo = 'ok', accion) => {
        let cont = $('.ui-toasts');
        if (!cont) { cont = document.createElement('div'); cont.className = 'ui-toasts'; document.body.appendChild(cont); }
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
            <div class="ico ${peligro ? 'red' : 'aqua'}"><i class="bi bi-${icono || (peligro ? 'trash3' : 'question-lg')}"></i></div>
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
            else if (e.key === 'Escape') { lista.hidden = true; }
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
    EV.drawer = (el) => {
        let bd = $('.ui-backdrop[data-drawer]');
        if (!bd) { bd = document.createElement('div'); bd.className = 'ui-backdrop'; bd.dataset.drawer = '1'; document.body.appendChild(bd); }
        const api = {
            abrir() { bd.classList.add('open'); el.classList.add('open'); document.body.style.overflow = 'hidden'; },
            cerrar() { bd.classList.remove('open'); el.classList.remove('open'); document.body.style.overflow = ''; },
            abierto: () => el.classList.contains('open')
        };
        bd.addEventListener('click', () => api.cerrar());
        return api;
    };

    window.EV = EV;
})();
