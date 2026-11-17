# «Circuitos por el mundo» (Yourttoo) elegidos para la Home — guía para la web

> CAMBIO 133 de `Database/EUROVIP_BaseDeDatos.sql` · aplicado en el servidor 93 (`TropitoursVDT`, esquema `Eurovip`) · 17/11/2026
> Integración de Yourttoo en la web y la API: `EuroVip/docs/08-integracion-yourttoo.md` (esta guía cubre solo lo que se elige para la Home).

## Qué hace el BackOffice

`/Circuitos/Index` › pestaña **«Circuitos por el mundo»**:

- Lista los destinos (países) de Yourttoo.
- Al abrir uno, muestra sus circuitos del **más económico al más caro**, en tarjetas.
- Con «Agregar a la Home» se elige un circuito. Arriba se ven los elegidos **agrupados por destino**, con flechas para
  ordenarlos y × para quitarlos.

Cada circuito elegido se guarda como una fila de **`Eurovip.inicio`** (tabla intermedia) con `tipo_servicio = 'CIRCUITO_MUNDO'`:

| Columna de `inicio` | Qué guarda |
|---|---|
| `programa_fuente` | `'YOURTTOO'` |
| `programa_codigo` | Código del programa en Yourttoo (**distingue mayúsculas**: va tal cual en `/circuitos/mundo/<codigo>`) |
| `id_paisDestino` / `id_destino` | ISO del país en mayúsculas (`TR`) / nombre del destino («Turquía») |
| `descripcion` | Título del programa |
| `caratulaDestino` | Foto (URL de Yourttoo) |
| `programa_dias`, `programa_ciudades`, `programa_categoria` | Días, ciudades, categoría de hotel |
| `precioWeb` + `id_moneda` | Precio «desde» de Yourttoo **por persona en doble**, en su moneda (USD), **sin margen ni comisión** |
| `peso` | Orden dentro del destino (1 = primero) |

Van **sin sección** (`id_contenedor` vacío). Por eso `spPortalListarInicio` no las devuelve y la Home actual no cambia.

## Cómo leerlo

```sql
EXEC Eurovip.spPortalListarCircuitosMundo @Idioma = 'ES';
-- id, fuente, codigo, pais, destino, titulo, imagen, dias, ciudades, categoria, desde, moneda, peso
-- ordenado por destino, peso y precio
```

## Cómo usarlo en la fila «Circuitos por el mundo» de la Home

- Agrupar por `destino` (o `pais`). Cada grupo es un carrusel o bloque con sus circuitos en el orden de `peso`.
- Enlace de cada tarjeta: `/circuitos/mundo/<codigo>`. El enlace del destino: `/circuitos/mundo?pais=<pais en minúsculas>&orden=precio`.
- **Precio**: no mostrar `desde` tal cual. Con sesión, calcular el de la agencia con la fórmula de la integración
  (`importe × tipo de cambio × (1 + margen)` + comisión de la agencia, en RD$). Sin sesión, sin precio, como el resto de lo público.
  Si se quiere el precio exacto al día, volver a pedirlo a Yourttoo por el `codigo` (la ficha ya lo hace).
- Si la lista está vacía, mantener lo de hoy (los 5 más económicos en vivo).
