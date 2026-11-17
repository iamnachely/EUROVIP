# Pestañas del buscador — orden configurable desde el BackOffice

> CAMBIO 134 de `Database/EUROVIP_BaseDeDatos.sql` · aplicado en el servidor 93 (`TropitoursVDT`, esquema `Eurovip`) · 17/11/2026

## Qué pidió la usuaria

Poder cambiar las secciones (pestañas) del buscador de la web y su **orden**. Por ejemplo: Hoteles, Vuelos, Circuitos,
Excursiones, Traslados, Coches y Cruceros. También poder **ocultar** alguna y elegir **cuál se abre al entrar**.

## Dónde se configura (BackOffice)

`/HomeWeb/Index` (Home de la web) › bloque **«Buscador de la web»**, debajo del carrusel. Todo visual y se guarda solo:

- **Arrastrar** una pestaña la cambia de lugar. La de la izquierda sale primero.
- **Ojo**: la muestra o la oculta en la web. Siempre queda al menos una visible.
- **Estrella**: la pestaña que se abre al entrar. Solo una, y tiene que estar visible.

## Dónde se guarda

Tabla `Eurovip.buscador_pestana`:

| Columna | Tipo | Qué es |
|---|---|---|
| `clave` | `varchar(20)` PK | La pestaña de la web: `hoteles`, `vuelos`, `circuitos`, `excursiones`, `traslados`, `coches`, `cruceros` |
| `nombre` | `nvarchar(40)` | Texto de referencia (la web sigue usando sus traducciones de `es.json`) |
| `orden` | `smallint` | 1 = la primera |
| `activo` | `bit` | 0 = no se muestra |
| `predeterminada` | `bit` | 1 = se abre al entrar (solo una fila) |

Valores iniciales: los mismos que la web tiene hoy (ese orden, todas visibles y Hoteles por defecto). Hasta que alguien lo
cambie en el BackOffice, la web se ve igual.

## Cómo leerlo (web / API)

```sql
EXEC Eurovip.spPortalListarBuscadorPestanas;   -- clave, nombre, orden, activo, predeterminada (ordenado por orden)
```

Sugerencia de API: `GET /catalogo/buscador` → `[{ "clave": "hoteles", "orden": 1, "activo": true, "predeterminada": true }, …]`,
con caché corta (5–10 min). Es público: no lleva precios.

## Cómo aplicarlo en la web

Archivo: `web/src/features/catalogo/Buscador.tsx`. Hoy la lista `pestanas` está fija en el código:

```ts
const pestanas = [
  { valor: 'hoteles', … }, { valor: 'vuelos', … }, { valor: 'circuitos', … },
  ...(excursiones.length ? [{ valor: 'excursiones', … }] : []),
  ...(traslados.length ? [{ valor: 'traslados', … }] : []),
  { valor: 'coches', … },
  ...(cruceros ? [{ valor: 'cruceros', … }] : []),
]
const pestana = valores.tipo === 'CIRCUITO' ? 'circuitos' : 'hoteles'
```

Cambio propuesto:

1. Mantener ese catálogo (icono, texto y formulario de cada `valor`) **tal cual**. Lo único que viene de la base es el orden,
   si se ve y cuál se abre primero.
2. Ordenar y filtrar con lo que devuelve el procedimiento:
   ```ts
   const config = await obtenerBuscador()   // [{ clave, orden, activo, predeterminada }]
   const pos = new Map(config.map(c => [c.clave, c]))
   const visibles = pestanas
     .filter(p => pos.get(p.valor)?.activo ?? true)                       // sin dato → se ve (como hoy)
     .sort((a, b) => (pos.get(a.valor)?.orden ?? 99) - (pos.get(b.valor)?.orden ?? 99))
   ```
3. Pestaña que se abre:
   - si la URL pide una (`?tipo=CIRCUITO` → `circuitos`), esa, **aunque esté oculta en la configuración**: el enlace manda;
   - si no, la `predeterminada` (si está entre las visibles);
   - si no, la primera visible.
4. Las condiciones que ya existen se respetan: excursiones, traslados y cruceros siguen saliendo **solo si hay datos**,
   aunque estén activas en la configuración.
5. Si la llamada falla o la tabla está vacía → usar el orden de hoy (no se cae el buscador).
6. Aplicarlo igual en todos los sitios donde se pinte el buscador (home pública, `/hotel/destino`, portal) y en la app móvil
   si tiene el mismo selector.

## Pruebas sugeridas

- Mover «Circuitos» a la primera posición en el BackOffice → la web la muestra primero.
- Ocultar «Coches» → desaparece de la web. Volver a mostrarla → vuelve.
- Marcar «Circuitos» con la estrella → al entrar en la home se abre Circuitos. Con `?tipo=…` en la URL manda la URL.
- Base sin filas o API caída → orden de siempre.

## Deshacer

```sql
DROP PROCEDURE Eurovip.spPortalListarBuscadorPestanas; DROP TABLE Eurovip.buscador_pestana;
```
