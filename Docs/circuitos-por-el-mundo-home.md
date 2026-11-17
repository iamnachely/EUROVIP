# Circuitos por destino en la Home (Yourttoo + circuitos propios) — guía para la web

> CAMBIO 133, 135 de `Database/EUROVIP_BaseDeDatos.sql` · aplicados en el servidor 93 (`TropitoursVDT`, esquema `Eurovip`) · 17/11/2026
> Integración de Yourttoo en la web y la API: `docs/08-integracion-yourttoo.md`.

## Qué pidió la usuaria

- Los circuitos de la integración (Yourttoo) se eligen en el BackOffice **visualmente y agrupados por destino**.
- Los circuitos **manuales** (propios) y los de **Yourttoo** van en la **misma sección** de la Home (`CIRCUITOS`), **agrupados por destino**.
- Al hacer clic en un destino salen **todos** los circuitos de ese destino: los de Yourttoo elegidos y los manuales.

El destino es el **país** (código ISO de 2 letras):
- en Yourttoo, el país del programa;
- en los manuales, `Eurovip.circuito_destino.id_pais`.

## Qué hace el BackOffice

En `/Circuitos/Index`, pestaña **«Circuitos por el mundo»**, se elige cada programa de Yourttoo con «Agregar a la Home» y se
ordena dentro de su destino. Por cada destino con al menos un programa elegido, el BackOffice **mantiene solo** una tarjeta en
`Eurovip.inicio`:

| | Tarjeta del destino | Circuito de Yourttoo elegido |
|---|---|---|
| `tipo_servicio` | `CIRCUITO_MUNDO` | `CIRCUITO_MUNDO` |
| `programa_codigo` | **NULL** | Código del programa (distingue mayúsculas) |
| Sección (`id_contenedor`) | La de los circuitos: **CIRCUITOS** | Ninguna (lista interna del destino) |
| `estado` | Se crea **oculta** (0); se enciende con el ojo en Home de la web | 0 |
| `descripcion` | «Circuitos en Japón» | Título del programa |
| `caratulaDestino` | Foto del **primer** circuito elegido | Foto del programa |
| `precioWeb` + `id_moneda` | El «desde» **más bajo** de los elegidos (USD, sin margen) | «Desde» del programa |
| `programa_categoria` | «4 circuitos» (Yourttoo elegidos + propios del país con salidas) | Categoría de hotel |
| `programa_ciudades` | Ciudades de sus circuitos | Ciudades del programa |
| `id_paisDestino` / `id_destino` | `JP` / «Japón» | `JP` / «Japón» |

La tarjeta se rehace al agregar, quitar u ordenar. Si el destino se queda sin programas de Yourttoo, la tarjeta se borra.

## 1. Tarjeta en la Home (sección CIRCUITOS)

`spPortalListarInicio` ya la devuelve (está en la sección CIRCUITOS) con `tipo_servicio = 'CIRCUITO_MUNDO'`, `pais`
(`id_paisDestino`), `descripcion`, `imagen`.

- **Enlace**: `/circuitos/mundo?pais=<pais en minúsculas>&orden=precio`.
  - **Ya hecho** en `web/src/features/catalogo/SeccionesInicio.tsx` (`urlCircuitosDestino`).
  - Sin ese cambio publicado, la tarjeta llevaría a la búsqueda de hoteles.
- **Precio**: no mostrar `precioWeb` tal cual (es de Yourttoo, sin margen).
  - Con sesión: calcular el de la agencia, o mostrar «Ver circuitos».
  - Sin sesión: sin precio, como todo lo público.
- **Subtítulo sugerido**: `programa_categoria` («4 circuitos»).

## 2. Página del destino: todos sus circuitos

```sql
EXEC Eurovip.spPortalListarCircuitosDestino @pais = 'JP', @Idioma = 'ES';
-- fuente ('YOURTTOO' | 'PROPIO'), codigo, titulo, imagen, dias, ciudades, desde, moneda, peso
-- ordenado: peso (1 = primero) y después precio
```

| `fuente` | `codigo` | Enlace de la tarjeta | Precio |
|---|---|---|---|
| `YOURTTOO` | código del programa | `/circuitos/mundo/<codigo>` (tal cual, distingue mayúsculas) | `desde` en USD sin margen → fórmula de la integración |
| `PROPIO` | nombre del circuito | la ficha de circuito propia (`urlCircuitos(nombre)`) | `desde` = precio web (`fnPrecioWeb`) de la próxima salida activa, en su moneda → como hoy en circuitos |

- Los propios solo salen si tienen salidas **activas desde mañana**.
- Hoy `/circuitos/mundo?pais=…` lista en vivo **todo** Yourttoo del país. La propuesta es que, cuando hay selección del
  BackOffice, muestre arriba **«Recomendados»** (este procedimiento: elegidos + propios) y debajo, opcionalmente, el resto
  de Yourttoo en vivo.

## 3. Solo los elegidos de Yourttoo (si hace falta)

```sql
EXEC Eurovip.spPortalListarCircuitosMundo @Idioma = 'ES';   -- por destino, peso y precio (solo Yourttoo)
```
