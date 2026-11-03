/* =============================================================================
   EuroVip · Todos los <select> con buscador (Tom Select)
   -----------------------------------------------------------------------------
   Convierte TODOS los <select> de la página en Tom Select: el usuario escribe y
   la lista se va filtrando (sin importar tildes ni mayúsculas).

   - También los que se crean después por JavaScript (tablas, modales, filas…).
   - El <select> original sigue en la página (oculto) y es el que manda:
       · leer sel.value / selectedOptions funciona igual que antes;
       · sel.value = 'x', sel.selectedIndex = n, opcion.selected = true,
         sel.innerHTML = '<option…>', sel.add(…), sel.disabled = true,
         form.reset(), ocultar el select… → el buscador se actualiza solo;
       · al elegir, se disparan los eventos 'input' y 'change' del <select>
         (los onchange y addEventListener de cada pantalla siguen funcionando).
   - Se ve igual que el select original: copia su borde, fondo, tamaño y ancho.
   - Para dejar uno sin buscador: atributo data-no-buscar en el select o en un
     contenedor.
   - Si una pantalla crea su propio TomSelect (Flyer/Editor), se respeta.
   ============================================================================= */
(function () {
    'use strict';
    if (!window.TomSelect || window.EVSelect) return;

    const TS = window.TomSelect;
    const vivos = new Set();          // selects convertidos por este archivo
    const pendientes = new Set();     // selects a revisar en el próximo microtask
    let programado = false;

    // ---------- Firma de las opciones (para saber si el código las cambió) ----------
    function firma(sel) {
        let s = '';
        for (const o of sel.options) {
            const g = o.parentNode && o.parentNode.tagName === 'OPTGROUP' ? o.parentNode.label : '';
            s += o.value + '\x01' + o.text + '\x01' + (o.disabled ? 1 : 0) + '\x01' + g + '\x02';
        }
        return s;
    }
    function valorNativo(sel) {
        return Array.from(sel.options).filter(o => o.selected).map(o => o.value);
    }
    const iguales = (a, b) => a.length === b.length && a.every((v, i) => v === b[i]);

    // ---------- Tom Select que nunca reordena ni toca de más el <select> original ----------
    class EVTomSelect extends TS {
        updateOriginalInput(opts = {}) {
            if (this.__evLeyendo) return; // estamos copiando DEL select: no escribir de vuelta
            const sel = this.input;
            if (!this.is_select_tag) return super.updateOriginalInput(opts);
            const items = this.items.slice();
            this.__evEscribiendo = true;
            try {
                // Opciones que Tom Select conoce pero el select no (no debería pasar): se agregan
                items.forEach(v => {
                    if (!Array.from(sel.options).some(o => o.value === v)) {
                        const d = this.options[v];
                        sel.append(new Option(d ? d[this.settings.labelField] : v, v));
                    }
                });
                if (sel.multiple) {
                    const set = new Set(items);
                    for (const o of sel.options) o.selected = set.has(o.value);
                } else if (items.length) {
                    const o = Array.from(sel.options).find(x => x.value === items[0]);
                    if (o) o.selected = true;
                } else {
                    const vacia = Array.from(sel.options).find(x => x.value === '');
                    if (vacia) vacia.selected = true; else sel.selectedIndex = -1;
                }
                this.__evFirma = firma(sel);
            } finally { this.__evEscribiendo = false; }
            if (this.isSetup && !opts.silent) this.trigger('change', this.getValue());
        }
    }

    // ---------- Copiar el aspecto del select original ----------
    function medir(sel) {
        const cs = getComputedStyle(sel);
        const visible = sel.getClientRects().length > 0;
        const r = visible ? sel.getBoundingClientRect() : null;
        const px = v => parseFloat(v) || 0;

        const envoltura = {
            display: /inline/.test(cs.display) || cs.display === 'none' ? 'inline-block' : cs.display === 'flex' || cs.display === 'grid' ? 'block' : cs.display,
            marginTop: cs.marginTop, marginRight: cs.marginRight, marginBottom: cs.marginBottom, marginLeft: cs.marginLeft,
            verticalAlign: cs.verticalAlign,
            flexGrow: cs.flexGrow, flexShrink: cs.flexShrink, flexBasis: cs.flexBasis, alignSelf: cs.alignSelf, order: cs.order,
            gridColumnStart: cs.gridColumnStart, gridColumnEnd: cs.gridColumnEnd, gridRowStart: cs.gridRowStart, gridRowEnd: cs.gridRowEnd,
        };
        if (cs.maxWidth !== 'none') envoltura.maxWidth = cs.maxWidth;
        if (px(cs.minWidth)) envoltura.minWidth = cs.minWidth;

        // Ancho: el que dice su CSS (100%, 240px, auto…), no lo que mide justo ahora
        const padre = sel.parentElement;
        const pcs = padre ? getComputedStyle(padre) : null;
        const enFila = pcs && /flex/.test(pcs.display) && !/column/.test(pcs.flexDirection);
        const antes = sel.style.display;
        sel.style.display = 'none';                      // sin caja, el navegador devuelve el valor escrito en el CSS
        const declarado = getComputedStyle(sel).width;
        sel.style.display = antes;
        if (enFila && px(cs.flexGrow) > 0) { envoltura.width = /%|px/.test(declarado) ? declarado : 'auto'; envoltura.minWidth = envoltura.minWidth || '0'; }
        else if (/%|px/.test(declarado)) envoltura.width = declarado;
        else if (pcs && /grid/.test(pcs.display) && cs.justifySelf !== 'start') envoltura.width = '100%';
        else { envoltura.width = 'auto'; if (visible && !envoltura.minWidth) envoltura.minWidth = Math.ceil(r.width) + 'px'; }
        if (envoltura.width === '100%' && !enFila) envoltura.display = 'block';

        let alto = visible ? r.height : px(cs.height);
        if (!alto) alto = Math.round(px(cs.fontSize) * 1.3 + px(cs.paddingTop) + px(cs.paddingBottom) + px(cs.borderTopWidth) + px(cs.borderBottomWidth));
        const control = {
            borderTop: `${cs.borderTopWidth} ${cs.borderTopStyle} ${cs.borderTopColor}`,
            borderRight: `${cs.borderRightWidth} ${cs.borderRightStyle} ${cs.borderRightColor}`,
            borderBottom: `${cs.borderBottomWidth} ${cs.borderBottomStyle} ${cs.borderBottomColor}`,
            borderLeft: `${cs.borderLeftWidth} ${cs.borderLeftStyle} ${cs.borderLeftColor}`,
            borderRadius: cs.borderRadius, backgroundColor: cs.backgroundColor, color: cs.color,
            fontFamily: cs.fontFamily, fontSize: cs.fontSize, fontWeight: cs.fontWeight,
            paddingTop: cs.paddingTop, paddingBottom: cs.paddingBottom, paddingLeft: cs.paddingLeft,
            paddingRight: sel.multiple ? cs.paddingRight : Math.max(px(cs.paddingRight), 30) + 'px',
            minHeight: alto ? alto + 'px' : '',
        };
        return { envoltura, control, oculto: cs.display === 'none' };
    }

    function aplicarVisibilidad(sel) {
        const ts = sel.tomselect;
        if (!ts) return;
        const oculto = sel.hidden || getComputedStyle(sel).display === 'none';
        ts.wrapper.style.display = oculto ? 'none' : (ts.__evDisplay || '');
        if (oculto && ts.isOpen) ts.close();
    }

    // ---------- Convertir un select ----------
    function convertir(sel) {
        if (!sel.isConnected || sel.tomselect || sel.closest('[data-no-buscar]') || sel.closest('template')) return;
        // Copia de un select ya convertido (cloneNode): limpiar lo copiado
        if (sel.classList.contains('tomselected')) {
            sel.classList.remove('tomselected', 'ts-hidden-accessible');
            const sig = sel.nextElementSibling;
            if (sig && sig.classList.contains('ts-wrapper')) sig.remove();
            if (sel.getAttribute('tabindex') === '-1') sel.removeAttribute('tabindex');
        }
        const m = medir(sel);
        const clases = Array.from(sel.classList);
        let ts;
        try {
            ts = new EVTomSelect(sel, {
                create: false,
                allowEmptyOption: true,
                maxOptions: 500,
                closeAfterSelect: !sel.multiple,
                hideSelected: sel.multiple,
                selectOnTab: true,
                dropdownParent: 'body',
                wrapperClass: 'ts-wrapper ev-ts',
                plugins: sel.multiple ? ['remove_button'] : [],
                placeholder: sel.getAttribute('placeholder') || sel.dataset.placeholder || (sel.multiple ? 'Escribe para buscar…' : undefined),
                render: {
                    no_results: () => '<div class="no-results">Sin resultados</div>',
                },
            });
        } catch (e) { console.warn('EVSelect: no se pudo convertir', sel, e); return; }

        ts.__evAuto = true;
        ts.__evFirma = firma(sel);
        // Las clases del select son para el select; la caja nueva copia el aspecto ya calculado
        clases.forEach(c => ts.wrapper.classList.remove(c));
        Object.assign(ts.wrapper.style, m.envoltura);
        ts.__evDisplay = m.envoltura.display;
        Object.assign(ts.control.style, m.control);
        if (m.oculto) ts.wrapper.style.display = 'none';
        if (sel.title) ts.wrapper.title = sel.title;

        // Escribir para buscar: al abrir, que se vea toda la lista y el texto esté listo
        ts.on('dropdown_open', () => { revisar(sel); ts.positionDropdown(); });
        ts.on('type', txt => ts.wrapper.classList.toggle('ev-escribiendo', !!txt));
        ts.on('dropdown_close', () => ts.wrapper.classList.remove('ev-escribiendo'));
        ts.on('change', () => ts.wrapper.classList.remove('ev-escribiendo'));

        // Cambios hechos por el código en las opciones, deshabilitar u ocultar
        const mo = new MutationObserver(lista => {
            for (const x of lista) {
                if (x.target === sel && x.type === 'attributes') {
                    if (x.attributeName === 'disabled') {
                        // Tom Select vuelve a escribir 'disabled' al (des)habilitarse: solo actuar si de verdad cambió
                        if (sel.disabled !== !!ts.isDisabled) sel.disabled ? ts.disable() : ts.enable();
                    }
                    else aplicarVisibilidad(sel);
                } else if (!ts.__evEscribiendo) pedirRevision(sel);
            }
        });
        mo.observe(sel, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ['disabled', 'label', 'value', 'hidden', 'style', 'class'] });
        ts.__evMo = mo;
        vivos.add(sel);
    }

    // ---------- Sincronizar desde el select original ----------
    // Lee opciones, grupos y lo elegido igual que Tom Select al crearse (incluida la opción vacía, p. ej. «Todos»)
    function leerOpciones(sel) {
        const opciones = [], grupos = [], items = [], vistos = new Set();
        let orden = 0, ng = 0;
        const agregar = (o, g) => {
            if (o.selected) items.push(o.value);
            if (vistos.has(o.value)) return;
            vistos.add(o.value);
            opciones.push(Object.assign({}, o.dataset, { value: o.value, text: o.textContent, disabled: o.disabled, optgroup: g, $option: o, $order: ++orden }));
        };
        for (const h of sel.children) {
            if (h.tagName === 'OPTGROUP') {
                const id = 'g' + (++ng);
                grupos.push({ value: id, label: h.label || '', disabled: h.disabled, $order: ++orden });
                for (const o of h.children) if (o.tagName === 'OPTION') agregar(o, id);
            } else if (h.tagName === 'OPTION') agregar(h);
        }
        return { opciones, grupos, items };
    }
    function revisar(sel) {
        const ts = sel.tomselect;
        if (!ts || !ts.__evAuto || ts.__evEscribiendo || ts.__evLeyendo) return;
        // Freno de seguridad: nunca más de 20 revisiones seguidas del mismo select sin respirar
        const ahora = performance.now();
        if (ahora - (ts.__evT || 0) > 50) { ts.__evT = ahora; ts.__evN = 0; }
        if (++ts.__evN > 20) return;
        const f = firma(sel);
        if (f !== ts.__evFirma) {
            ts.__evLeyendo = true;
            try {
                ts.clear(true);
                ts.clearOptions(() => false);
                ts.clearOptionGroups();
                const l = leerOpciones(sel);
                l.grupos.forEach(g => ts.registerOptionGroup(g));
                ts.addOptions(l.opciones);
                ts.setValue(l.items, true);
            } finally { ts.__evLeyendo = false; }
            ts.__evFirma = f;
            if (ts.isOpen) ts.refreshOptions(false);
            return;
        }
        const nat = valorNativo(sel);
        const conocidos = nat.filter(v => ts.options[v] !== undefined);
        if (!iguales(conocidos.slice().sort(), ts.items.slice().sort())) {
            ts.__evLeyendo = true;
            try { ts.setValue(conocidos, true); } finally { ts.__evLeyendo = false; }
        }
    }
    function pedirRevision(sel) {
        const ts = sel.tomselect;
        if (ts && (ts.__evLeyendo || ts.__evEscribiendo)) return;
        pendientes.add(sel);
        if (programado) return;
        programado = true;
        queueMicrotask(() => {
            programado = false;
            const l = Array.from(pendientes); pendientes.clear();
            l.forEach(s => s.tomselect ? revisar(s) : convertir(s));
        });
    }

    // sel.value = …, sel.selectedIndex = …, opcion.selected = … → el buscador se entera al momento
    function engancharSetter(proto, prop, selDe) {
        const d = Object.getOwnPropertyDescriptor(proto, prop);
        if (!d || !d.set) return;
        Object.defineProperty(proto, prop, Object.assign({}, d, {
            set(v) {
                d.set.call(this, v);
                const sel = selDe(this);
                const ts = sel && sel.tomselect;
                if (ts && ts.__evAuto && !ts.__evEscribiendo && !ts.__evLeyendo) pedirRevision(sel);
            },
        }));
    }
    engancharSetter(HTMLSelectElement.prototype, 'value', el => el);
    engancharSetter(HTMLSelectElement.prototype, 'selectedIndex', el => el);
    engancharSetter(HTMLOptionElement.prototype, 'selected', el => el.parentElement && (el.parentElement.tagName === 'SELECT' ? el.parentElement : el.parentElement.parentElement));
    // sel.focus() → enfoca el buscador
    const focoNativo = HTMLSelectElement.prototype.focus;
    HTMLSelectElement.prototype.focus = function () {
        if (this.tomselect && this.tomselect.__evAuto) return this.tomselect.focus();
        return focoNativo.apply(this, arguments);
    };
    // form.reset() devuelve los selects a su valor inicial
    document.addEventListener('reset', e => setTimeout(() => e.target.querySelectorAll && e.target.querySelectorAll('select').forEach(revisar)), true);

    // Si una pantalla crea su propio TomSelect sobre un select ya convertido, se le deja el sitio
    window.TomSelect = class extends TS {
        constructor(el, settings) {
            const e = typeof el === 'string' ? document.querySelector(el) : el;
            if (e && e.tomselect && e.tomselect.__evAuto) quitar(e);
            super(e || el, settings);
        }
    };
    Object.keys(TS).forEach(k => { try { window.TomSelect[k] = TS[k]; } catch (_) { } });

    function quitar(sel) {
        const ts = sel.tomselect;
        if (!ts) return;
        if (ts.__evMo) ts.__evMo.disconnect();
        vivos.delete(sel);
        const val = valorNativo(sel);
        ts.revertSettings.innerHTML = sel.innerHTML; // destroy() deja las opciones de ahora, no las del principio
        ts.__evLeyendo = true;
        ts.destroy();
        for (const o of sel.options) o.selected = val.includes(o.value);
    }

    // ---------- Selects nuevos, movidos o quitados ----------
    function buscarEn(nodo, fn) {
        if (nodo.nodeType !== 1) return;
        if (nodo.tagName === 'SELECT') fn(nodo);
        else if (nodo.firstElementChild) nodo.querySelectorAll('select').forEach(fn);
    }
    let listo = false;
    new MutationObserver(lista => {
        if (!listo) return; // al cargar, los convierte convertirTodos()
        for (const x of lista) {
            x.addedNodes.forEach(n => buscarEn(n, sel => {
                const ts = sel.tomselect;
                if (ts && ts.__evAuto) { if (sel.nextElementSibling !== ts.wrapper) sel.after(ts.wrapper); }
                else if (!ts) pedirRevision(sel);
            }));
            x.removedNodes.forEach(n => buscarEn(n, sel => queueMicrotask(() => {
                const ts = sel.tomselect;
                if (ts && ts.__evAuto && !sel.isConnected) { ts.close(); ts.wrapper.remove(); }
            })));
        }
    }).observe(document.documentElement, { childList: true, subtree: true });

    function convertirTodos(raiz) { listo = true; (raiz || document).querySelectorAll('select').forEach(convertir); }
    // Red de seguridad: cambios que no avisan (p. ej. una opción marcada con setAttribute('selected'))
    setInterval(() => vivos.forEach(sel => sel.isConnected && revisar(sel)), 1500);

    window.EVSelect = { convertir, convertirTodos, revisar, quitar };
    // Después de los scripts de cada pantalla (que pueden crear o llenar selects al cargar)
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => setTimeout(convertirTodos));
    else setTimeout(convertirTodos);
})();
