// Carrusel de fotos a pantalla completa (fotos de un hotel, destino…): foto grande, flechas, miniaturas y contador.
//   EVCarrusel.abrir({ fotos: [{ url, texto }], titulo, inicio = 0 })
// Teclado: ← → para pasar, Esc para cerrar. En el móvil se desliza con el dedo. Usa EV (eurovip-ui.js).
window.EVCarrusel = (() => {
    const css = document.createElement('style');
    css.textContent = `
.evc-fondo { position: fixed; inset: 0; z-index: 3000; background: rgba(6,18,22,.92); display: grid; grid-template-rows: auto minmax(0,1fr) auto; animation: evcIn .18s ease-out; color: #fff; }
@keyframes evcIn { from { opacity: 0 } }
.evc-cab { display: flex; align-items: center; gap: 12px; padding: 14px 18px; }
.evc-cab h3 { margin: 0; font: 800 17px var(--ev-font, system-ui); min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.evc-cab .evc-n { font: 700 13px var(--ev-font, system-ui); opacity: .75; white-space: nowrap; }
.evc-cab button { margin-left: auto; }
.evc-btn { border: 0; width: 44px; height: 44px; border-radius: 50%; display: grid; place-items: center; font-size: 20px; cursor: pointer;
    background: rgba(255,255,255,.12); color: #fff; transition: background .15s, transform .15s; flex-shrink: 0; }
.evc-btn:hover { background: rgba(255,255,255,.24); transform: scale(1.06); }
.evc-btn:focus-visible { outline: 2px solid #5EEAD4; outline-offset: 2px; }
.evc-escena { position: relative; display: grid; grid-template-columns: 64px minmax(0,1fr) 64px; align-items: center; min-height: 0; padding: 0 8px; }
.evc-foto { display: grid; place-items: center; height: 100%; min-height: 0; }
.evc-foto img { max-width: 100%; max-height: 100%; object-fit: contain; border-radius: 14px; box-shadow: 0 20px 60px -20px rgba(0,0,0,.7); animation: evcFoto .25s ease-out; user-select: none; }
@keyframes evcFoto { from { opacity: 0; transform: scale(.985) } }
.evc-foto .evc-mal { opacity: .7; font-weight: 700; }
.evc-texto { text-align: center; font: 600 13px var(--ev-font, system-ui); opacity: .85; padding: 6px 16px 0; min-height: 22px; }
.evc-tiras { display: flex; gap: 8px; overflow-x: auto; padding: 10px 18px 16px; justify-content: center; scrollbar-width: thin; }
.evc-tiras button { border: 2px solid transparent; padding: 0; border-radius: 10px; overflow: hidden; width: 74px; height: 52px; flex-shrink: 0; cursor: pointer; background: rgba(255,255,255,.08); opacity: .6; transition: opacity .15s, border-color .15s; }
.evc-tiras button.on, .evc-tiras button:hover { opacity: 1; border-color: #5EEAD4; }
.evc-tiras img { width: 100%; height: 100%; object-fit: cover; display: block; }
.evc-vacio { display: grid; place-items: center; text-align: center; gap: 8px; font-weight: 700; opacity: .85; }
.evc-vacio i { font-size: 44px; }
@media (max-width: 640px) { .evc-escena { grid-template-columns: 1fr; } .evc-escena > .evc-btn { position: absolute; top: 50%; transform: translateY(-50%); z-index: 1; }
    .evc-escena > .evc-btn.izq { left: 8px; } .evc-escena > .evc-btn.der { right: 8px; } .evc-tiras { justify-content: flex-start; } }`;
    document.head.appendChild(css);

    function abrir({ fotos = [], titulo = 'Fotos', inicio = 0, vacio = 'Todavía no tiene fotos.' } = {}) {
        const lista = fotos.filter(f => f?.url);
        let i = Math.min(Math.max(0, inicio), Math.max(0, lista.length - 1));
        const fondo = document.createElement('div');
        fondo.className = 'evc-fondo';
        fondo.setAttribute('role', 'dialog'); fondo.setAttribute('aria-modal', 'true'); fondo.setAttribute('aria-label', titulo);
        fondo.innerHTML = `<div class="evc-cab"><i class="bi bi-images" style="font-size:20px"></i><h3>${EV.esc(titulo)}</h3><span class="evc-n"></span>
                <button type="button" class="evc-btn" data-cerrar aria-label="Cerrar"><i class="bi bi-x-lg"></i></button></div>
            ${lista.length ? `<div class="evc-escena">
                <button type="button" class="evc-btn izq" data-paso="-1" aria-label="Foto anterior" ${lista.length < 2 ? 'hidden' : ''}><i class="bi bi-chevron-left"></i></button>
                <div class="evc-foto"></div>
                <button type="button" class="evc-btn der" data-paso="1" aria-label="Foto siguiente" ${lista.length < 2 ? 'hidden' : ''}><i class="bi bi-chevron-right"></i></button></div>
                <div><div class="evc-texto"></div><div class="evc-tiras">${lista.length > 1 ? lista.map((f, k) => `<button type="button" data-ir="${k}" aria-label="Foto ${k + 1}"><img src="${EV.esc(f.url)}" alt="" loading="lazy"></button>`).join('') : ''}</div></div>`
            : `<div class="evc-vacio"><div><i class="bi bi-image"></i><div>${EV.esc(vacio)}</div></div></div><div></div>`}`;
        document.body.appendChild(fondo);
        const $ = s => fondo.querySelector(s);
        const pintar = () => {
            if (!lista.length) { $('.evc-n').textContent = ''; return; }
            const f = lista[i];
            $('.evc-foto').innerHTML = `<img src="${EV.esc(f.url)}" alt="${EV.esc(f.texto || titulo)}" onerror="this.outerHTML='<span class=&quot;evc-mal&quot;>No se pudo cargar esta foto</span>'">`;
            $('.evc-n').textContent = `${i + 1} de ${lista.length}`;
            $('.evc-texto').textContent = f.texto || '';
            fondo.querySelectorAll('[data-ir]').forEach(b => b.classList.toggle('on', +b.dataset.ir === i));
            fondo.querySelector(`[data-ir="${i}"]`)?.scrollIntoView({ block: 'nearest', inline: 'center', behavior: 'smooth' });
            // precarga la siguiente
            if (lista.length > 1) new Image().src = lista[(i + 1) % lista.length].url;
        };
        const ir = n => { if (!lista.length) return; i = (n + lista.length) % lista.length; pintar(); };
        const cerrar = () => { fondo.remove(); document.removeEventListener('keydown', tecla); };
        const tecla = e => {
            if (e.key === 'Escape') cerrar();
            else if (e.key === 'ArrowRight') ir(i + 1);
            else if (e.key === 'ArrowLeft') ir(i - 1);
        };
        document.addEventListener('keydown', tecla);
        fondo.addEventListener('click', e => {
            if (e.target.closest('[data-cerrar]') || e.target === fondo) return cerrar();
            const p = e.target.closest('[data-paso]'); if (p) return ir(i + +p.dataset.paso);
            const t = e.target.closest('[data-ir]'); if (t) return ir(+t.dataset.ir);
        });
        let x0 = null;
        fondo.addEventListener('touchstart', e => { x0 = e.touches[0].clientX; }, { passive: true });
        fondo.addEventListener('touchend', e => { if (x0 == null) return; const d = e.changedTouches[0].clientX - x0; if (Math.abs(d) > 45) ir(i + (d < 0 ? 1 : -1)); x0 = null; });
        pintar();
        setTimeout(() => $('[data-cerrar]').focus(), 30);
    }

    return { abrir };
})();
