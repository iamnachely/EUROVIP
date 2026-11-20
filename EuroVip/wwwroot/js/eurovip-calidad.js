/* Calidad de la imagen al subir (pedido de la usuaria, 20/11/2026): SIEMPRE que se elige una foto (en Hoteles, Ofertas, Paquetes, Circuitos,
   Excursiones, Traslados, Cruceros, Catálogos, Perfil… cualquier selector de imágenes) se le dice a la persona si su calidad es BUENA, NORMAL
   o MALA. Nunca se bloquea ni se toca la imagen: el servidor la guarda sin perder calidad (sin reducir ni recomprimir). Un solo script
   para todas las pantallas: escucha «change» y «drop» de cualquier input de archivos de imágenes. */
(function () {
    'use strict';
    if (window.EVCalidad) return;

    var LADO_BUENA = 1600, CORTO_BUENA = 900;      // buena: lado largo ≥ 1600 px y corto ≥ 900 px
    var LADO_NORMAL = 1000, CORTO_NORMAL = 600;    // normal: lado largo ≥ 1000 px y corto ≥ 600 px; menos = mala
    var NIVELES = {
        buena:  { texto: 'Buena',  icono: '✔', msg: 'Se verá nítida en la web.' },
        normal: { texto: 'Normal', icono: '●', msg: 'Se ve bien, pero en pantallas grandes puede notarse. Lo ideal es de 1600 px de lado o más.' },
        mala:   { texto: 'Mala',   icono: '✖', msg: 'Se verá borrosa o pixelada en la web. Sube una más grande (mínimo 1000 px de lado, lo ideal 1600 px o más).' }
    };

    function estilo() {
        if (document.getElementById('evCalidadCss')) return;
        var s = document.createElement('style'); s.id = 'evCalidadCss';
        s.textContent =
            '.ev-cal-chip{display:inline-flex;align-items:center;gap:6px;margin:4px 0 0 8px;padding:4px 11px;border-radius:99px;font:700 12.5px/1.2 inherit;font-family:inherit;vertical-align:middle;border:1.5px solid}' +
            '.ev-cal-chip.buena{background:#e3f9ee;color:#0b6b46;border-color:#9be3c1}.ev-cal-chip.normal{background:#fff4d6;color:#8a5a00;border-color:#f3d37a}.ev-cal-chip.mala{background:#fde7e7;color:#a1241b;border-color:#f3a6a1}' +
            '#evCalPanel{position:fixed;left:18px;bottom:18px;z-index:99999;display:grid;gap:10px;width:min(420px,calc(100vw - 36px));pointer-events:none}' +
            '.ev-cal-card{pointer-events:auto;background:#fff;border-radius:18px;padding:14px 16px;box-shadow:0 14px 40px rgba(11,18,21,.28);border-left:8px solid;display:grid;gap:6px;font-family:inherit;animation:evCalIn .25s both}' +
            '.ev-cal-card.buena{border-color:#16a36a}.ev-cal-card.normal{border-color:#f0a800}.ev-cal-card.mala{border-color:#d92d20}' +
            '.ev-cal-top{display:flex;align-items:center;gap:10px}.ev-cal-nivel{font-weight:800;font-size:15px}' +
            '.ev-cal-card.buena .ev-cal-nivel{color:#0b6b46}.ev-cal-card.normal .ev-cal-nivel{color:#8a5a00}.ev-cal-card.mala .ev-cal-nivel{color:#a1241b}' +
            '.ev-cal-x{margin-left:auto;border:0;background:#eef2f3;border-radius:50%;width:26px;height:26px;cursor:pointer;font-size:14px;line-height:1}' +
            '.ev-cal-det{font-size:12.5px;color:#4a5b63;font-weight:600;word-break:break-word}.ev-cal-msg{font-size:13.5px;color:#0b1215;font-weight:600}' +
            '.ev-cal-ok{font-size:12px;color:#4a5b63}' +
            '@keyframes evCalIn{from{transform:translateY(14px);opacity:0}}';
        document.head.appendChild(s);
    }

    function medir(file) {
        return new Promise(function (ok) {
            function fin(w, h) { ok({ w: w, h: h }); }
            if (window.createImageBitmap) {
                createImageBitmap(file).then(function (b) { var r = { w: b.width, h: b.height }; if (b.close) b.close(); ok(r); }, viaImg);
            } else viaImg();
            function viaImg() {
                var u = URL.createObjectURL(file), im = new Image();
                im.onload = function () { URL.revokeObjectURL(u); fin(im.naturalWidth, im.naturalHeight); };
                im.onerror = function () { URL.revokeObjectURL(u); ok(null); };
                im.src = u;
            }
        });
    }

    function kb(b) { return b >= 1048576 ? (b / 1048576).toFixed(1).replace('.', ',') + ' MB' : Math.max(1, Math.round(b / 1024)) + ' KB'; }

    function evaluar(file, dim) {
        if (!dim) return { nivel: null, nombre: file.name, detalle: 'No se pudo medir esta imagen (¿formato raro?).' };
        var largo = Math.max(dim.w, dim.h), corto = Math.min(dim.w, dim.h);
        var nivel = (largo >= LADO_BUENA && corto >= CORTO_BUENA) ? 'buena' : (largo >= LADO_NORMAL && corto >= CORTO_NORMAL) ? 'normal' : 'mala';
        var nota = '';
        // Una foto grande pero muy comprimida (JPG) se ve con bloques: de buena pasa a normal (nunca a mala solo por esto)
        var esJpg = /jpe?g/i.test(file.type) || /\.jpe?g$/i.test(file.name);
        var bpp = file.size / (dim.w * dim.h);
        if (esJpg && bpp < 0.035 && nivel === 'buena') { nivel = 'normal'; nota = ' · muy comprimida (puede verse con bloques)'; }
        return { nivel: nivel, nombre: file.name, detalle: dim.w + '×' + dim.h + ' px · ' + kb(file.size) + nota };
    }

    function panel() {
        estilo();
        var p = document.getElementById('evCalPanel');
        if (!p) { p = document.createElement('div'); p.id = 'evCalPanel'; p.setAttribute('role', 'status'); p.setAttribute('aria-live', 'polite'); document.body.appendChild(p); }
        return p;
    }

    function mostrar(resultados, input) {
        var p = panel();
        // el aviso anterior de este mismo selector se quita
        if (input && input._evCalCard && input._evCalCard.parentNode) input._evCalCard.remove();
        if (input && input._evCalChip && input._evCalChip.parentNode) input._evCalChip.remove();
        var orden = { mala: 0, normal: 1, buena: 2 };
        var medidas = resultados.filter(function (r) { return r.nivel; });
        var peor = medidas.length ? medidas.slice().sort(function (a, b) { return orden[a.nivel] - orden[b.nivel]; })[0].nivel : null;
        var uno = resultados.length === 1;

        var card = document.createElement('div');
        card.className = 'ev-cal-card ' + (peor || 'normal');
        var titulo = uno
            ? '<span class="ev-cal-nivel">' + (peor ? NIVELES[peor].icono + ' Calidad de la imagen: ' + NIVELES[peor].texto.toUpperCase() : 'Calidad de la imagen: sin medir') + '</span>'
            : '<span class="ev-cal-nivel">' + resultados.length + ' imágenes · la peor es de calidad ' + (peor ? NIVELES[peor].texto.toUpperCase() : '—') + '</span>';
        var detalle = resultados.map(function (r) {
            var etq = r.nivel ? '<b>' + NIVELES[r.nivel].texto + '</b> — ' : '';
            return (uno ? '' : esc(r.nombre) + ': ') + etq + esc(r.detalle);
        }).join('<br>');
        card.innerHTML =
            '<div class="ev-cal-top">' + titulo + '<button type="button" class="ev-cal-x" aria-label="Cerrar">✕</button></div>' +
            (peor ? '<div class="ev-cal-msg">' + NIVELES[peor].msg + '</div>' : '') +
            '<div class="ev-cal-det">' + detalle + '</div>' +
            '<div class="ev-cal-ok">La imagen se guarda tal cual, sin perder calidad.</div>';
        card.querySelector('.ev-cal-x').addEventListener('click', function () { card.remove(); });
        p.appendChild(card);
        // buena y normal se van solas; mala se queda más tiempo para que se lea
        setTimeout(function () { if (card.parentNode) card.remove(); }, peor === 'mala' ? 25000 : 12000);

        if (input) {
            input._evCalCard = card;
            // y una etiqueta fija junto al botón, para que quede a la vista
            var chip = document.createElement('span');
            chip.className = 'ev-cal-chip ' + (peor || 'normal');
            chip.textContent = peor ? ('Calidad ' + NIVELES[peor].texto.toLowerCase() + (uno ? ' · ' + resultados[0].detalle.split(' · ')[0] : '')) : 'Calidad sin medir';
            var ancla = input.closest('label') || input;
            if (ancla.parentNode) ancla.insertAdjacentElement('afterend', chip);
            input._evCalChip = chip;
        }
    }

    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }

    function esImagen(f) { return /^image\//i.test(f.type) || /\.(jpe?g|png|webp|heic|gif|bmp)$/i.test(f.name); }

    function revisar(files, input) {
        var imgs = Array.prototype.filter.call(files || [], esImagen);
        if (!imgs.length) return;
        Promise.all(imgs.map(function (f) { return medir(f).then(function (d) { return evaluar(f, d); }); })).then(function (r) { mostrar(r, input); });
    }

    document.addEventListener('change', function (e) {
        var t = e.target;
        if (!t || t.tagName !== 'INPUT' || t.type !== 'file') return;
        if (!/image/i.test(t.getAttribute('accept') || '')) return;
        revisar(t.files, t);
    }, true);

    // Arrastrar y soltar fotos (los cuadros «Arrastra fotos aquí»)
    document.addEventListener('drop', function (e) {
        var f = e.dataTransfer && e.dataTransfer.files;
        if (f && f.length) revisar(f, null);
    }, true);

    window.EVCalidad = { evaluar: evaluar, medir: medir, revisar: revisar };
})();
