# Estado de las ofertas de hotel: «Confirmada» y «Bajo petición»

> Para: desarrollador de la web (repo EuroVip: `web/` y `api/`).
> De: BackOffice EUROVIP (`/Ofertas/Index`, carga de hoteles, y carga de proveedores).
> Base de datos: **TropitoursVDT**, esquema `Eurovip` (CAMBIO 131, ya aplicado).

## 1. Qué es, en una frase

Cada oferta de hotel (cada fecha de entrada) puede tener un **estado opcional**: **Confirmada** (hay disponibilidad asegurada) o
**Bajo petición** (hay que consultarla antes de confirmar). Si no tiene estado, no se muestra nada. En el BackOffice se ve como una
etiqueta de color junto al hotel: verde = Confirmada, naranja = Bajo petición, gris = otro estado.

La web debe **leer ese estado y mostrarlo igual** en la ficha del hotel y en las tarjetas/listas.

## 2. Dónde está en la base de datos

| Dato | Tabla / columna | Notas |
|---|---|---|
| Catálogo de estados | `Eurovip.estado_oferta` (`id_estado_oferta`, `nombre`, `orden`, `activo`) | De inicio: `1 = Confirmada`, `2 = Bajo petición`. Eurovip puede agregar más desde el BackOffice, así que **no fijes los ids**: lee el nombre. |
| Estado de cada oferta | `Eurovip.servicio.id_estado_oferta` (int, **opcional**) | `NULL` = sin estado. Una fila de `servicio` es una oferta de una fecha concreta. |

El estado es **por oferta (fecha)**, pero en el BackOffice se cambia normalmente **para todas las fechas del hotel** de una vez
(«Estado del hotel · todas sus fechas»). Por eso, lo normal es que todas las fechas de un hotel tengan el mismo estado, pero **puede haber
fechas con estado distinto** (el BackOffice lo muestra como «Varios estados»).

## 3. Cómo leerlo (SQL de ejemplo)

Estado de cada oferta de un hotel:

```sql
SELECT  s.id_servicio,
        sd.id_alojamiento,
        sd.desde                    AS fecha_entrada,
        s.id_estado_oferta,
        eo.nombre                   AS estado_oferta      -- NULL = sin estado
FROM    Eurovip.servicio s
JOIN    Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio AND sd.secuencia = 1
LEFT JOIN Eurovip.estado_oferta eo ON eo.id_estado_oferta = s.id_estado_oferta AND eo.activo = 1
WHERE   s.id_idioma = 'ES' AND s.activo = 1 AND sd.id_alojamiento = @hotel;
```

Estado del hotel en las tarjetas (resumen por hotel):

```sql
SELECT  sd.id_alojamiento,
        COUNT(DISTINCT ISNULL(s.id_estado_oferta, 0)) AS estados_distintos,   -- > 1 = hay mezcla
        MAX(eo.nombre)                                AS estado_oferta         -- el estado, si es uno solo
FROM    Eurovip.servicio s
JOIN    Eurovip.servicio_detalle sd ON sd.id_servicio = s.id_servicio AND sd.secuencia = 1
LEFT JOIN Eurovip.estado_oferta eo ON eo.id_estado_oferta = s.id_estado_oferta
WHERE   s.id_idioma = 'ES' AND s.activo = 1
GROUP BY sd.id_alojamiento;
```

> Ya existen los procedimientos y consultas de la web: **no se cambió ninguna tabla ni procedimiento existente**. La columna es
> nueva y opcional, así que lo que la web ya lee sigue funcionando igual.

## 4. Qué tiene que hacer la web

1. **API (`api/`)**: al devolver una oferta o un hotel, incluir `estadoOferta` (texto, `null` si no tiene) y, si se quiere, `idEstadoOferta`.
2. **Ficha del hotel (`web/`)**: junto a cada fecha/oferta (y/o en el encabezado del hotel) mostrar una etiqueta con el nombre del estado.
3. **Tarjetas y listas**: mostrar la etiqueta del estado en el hotel.
   - Si **todas** las fechas tienen el mismo estado, mostrarlo.
   - Si hay **mezcla**, mostrar la etiqueta por fecha en la ficha y nada (o «Varios estados») en la tarjeta.
4. **Si el estado es `NULL`: no mostrar nada.**
5. **Colores (los mismos del BackOffice)**, según el nombre:

   | Estado | Fondo | Texto |
   |---|---|---|
   | Confirmada (nombre contiene «confirm») | `#DCFCE7` | `#166534` |
   | Bajo petición (nombre contiene «petici») | `#FFEDD5` | `#9A3412` |
   | Cualquier otro estado | `#EEF2F6` | `#475467` |

   Etiqueta redonda (`border-radius: 999px`), texto en negrita de unos 11.5 px.
6. **Texto sugerido** para que el cliente entienda: «Confirmada» = disponibilidad asegurada; «Bajo petición» = se confirma al
   consultar (ej. un tooltip o línea pequeña bajo el botón de reservar).

## 5. Cómo se cambia en el BackOffice (para que la web use las mismas palabras)

- **Lista de hoteles** (`/Ofertas/Index`, vista «Por hotel»): cada tarjeta muestra la etiqueta del estado, y el filtro **«Todos los
  estados / Sin estado / Confirmada / Bajo petición»** filtra la lista.
- **Calendario del hotel**: campo **«Estado del hotel · todas sus fechas»** con las opciones *— Sin estado —*, *Confirmada*, *Bajo petición*
  (y las que se agreguen). Se aplica a todas las fechas del hotel; cada oferta también se puede editar sola.
- **Cargas** (manual, Excel y proveedores): el estado es **siempre opcional**; si no se pone, queda sin estado.
- Eurovip puede **agregar otros estados** desde la misma pantalla. Por eso la web debe mostrar **el nombre que venga**, no solo los dos de inicio.

## 6. Qué NO cambió

- Ninguna tabla ni procedimiento existente; `Eurovip.servicio` solo ganó la columna opcional `id_estado_oferta`.
- Los precios, las fechas y la disponibilidad se calculan igual que antes: **el estado es solo informativo**, no bloquea ni cambia precios.
- Las ofertas de antes quedan **sin estado** (`NULL`) hasta que Eurovip se lo ponga.
