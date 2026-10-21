# Cruceros — modelo de datos y pantalla

Base: **TropitoursVDT, esquema `Eurovip`, servidor de desarrollo (93)**. Script: FASE 21 (CAMBIO 67 a 77) en
`Database/EUROVIP_BaseDeDatos.sql`. **No está en producción**: no se tocó producción (servidores 29 ni .10).

## 1. De dónde sale el diseño

Se tomó del PDF «Resumen de Cruceros» y de la captura «Cubiertas y Cabinas». También se tomó de cómo
muestran un crucero las webs de venta habituales (Disney Cruise Line, MSC, Royal Caribbean, CruiseHive y
los motores tipo Traveltek/Widgety que usan las agencias). Todas siguen el mismo esquema:

| Lo que se ve en la web | Dónde se guarda |
|---|---|
| Tarjetas de categoría («Desde Rep. Dom.», «Cruceros Disney»…) — pantalla intermedia | `crucero_categoria` + `crucero_categoria_crucero` |
| Tarjeta de la lista: foto, fecha, duración, barco, desde/hasta, itinerario, precios Interior/Exterior/Balcón/Suite, logo naviera | `crucero` + `crucero_barco` + `crucero_naviera` + `vCruceroDesde` + foto PORTADA |
| Cabecera de la ficha: banner con el título, «Saliendo el … a bordo del …», slider, caja de datos, «Desde» | foto INTERMEDIA (banner), GALERIA, `crucero.subtitulo` (opcional) |
| Itinerario: #, fecha, puerto, info, llega, sale; «At Sea» | `crucero_itinerario` (la fecha = salida + día − 1) |
| «Tu barco – Disney Treasure»: pestañas (Comida y bebida, Entretenimiento…) con tarjetas y «Leer más» | `crucero_barco.descripcion` + `crucero_seccion` + `crucero_actividad` |
| «Elige tus fechas de viaje»: tarjetas con precio por categoría | `crucero_salida` + `crucero_precio` |
| «Elige el código de tarifa» (Florida Resident, BEST_PRICE) | `crucero_tarifa` |
| Cabinas por categoría (Inside/Outside/Suite) con foto, capacidad, m² y precio pp | `crucero_cabina_tipo` |
| Cubiertas y cabinas: cabina «Guaranteed», cubierta «?» | `crucero_cubierta` + `crucero_cabina` (sin cabina concreta = Garantizada) |

**Conceptos del sector**

- **Precio por persona (pp)**: se calcula con 2 personas compartiendo cabina. El total de la cabina para 2 es pp × 2.
- **Cabina garantizada**: se vende el tipo de cabina, y la naviera asigna el número y la cubierta después.
- **Código de tarifa**: la misma cabina en la misma fecha puede tener varios precios. Por ejemplo, el precio
  general (BEST_PRICE) o el de residentes de Florida.

## 2. Tablas

```
crucero_naviera ─< crucero_barco ─< crucero >─< crucero_categoria   (por crucero_categoria_crucero)
                                       │
          ┌──────────────┬─────────────┼───────────────┬──────────────────┐
  crucero_itinerario  crucero_seccion  crucero_cubierta  crucero_cabina_tipo  crucero_salida   crucero_tarifa
                         └< crucero_actividad   └──< crucero_cabina >──┘          │               │
                                                                                  └─ crucero_precio ─┘ (+ cabina_tipo)
```

| Tabla | Clave | Qué guarda |
|---|---|---|
| `crucero_naviera` | id_naviera | nombre (único), logo, activo |
| `crucero_barco` | id_barco | naviera, nombre (único por naviera), descripción («Tu barco»), imagen, activo |
| `crucero_categoria` | id_categoria | nombre (único), descripción, imagen, orden, activo (visible en la web) |
| `crucero` | id_crucero | título, subtítulo, barco, puertos de salida/llegada, noches (1-180), código del proveedor, descripción, incluye, no incluye, título de «Tu barco», moneda (DOP), activo, auditoría |
| `crucero_categoria_crucero` | (id_categoria, id_crucero) | en qué categorías sale cada crucero |
| `crucero_itinerario` | (id_crucero, dia) | puerto, en_navegacion, hora_llegada, hora_salida, descripción, imagen |
| `crucero_seccion` | id_seccion | título y orden de la pestaña |
| `crucero_actividad` | id_actividad | sección, título, descripción, imagen, orden |
| `crucero_cabina_tipo` | id_cabina_tipo | categoría (INTERIOR / EXTERIOR / BALCON / SUITE), código (11C…), nombre, descripción, capacidad mín/máx, m², imagen, permite_garantizada, orden |
| `crucero_cubierta` | id_cubierta | número (único por crucero), nombre, plano |
| `crucero_cabina` | id_cabina | tipo de cabina, cubierta (opcional), número (único por tipo), disponible |
| `crucero_salida` | id_salida | fecha de salida (única por crucero), activo. La llegada es la salida + noches |
| `crucero_tarifa` | id_tarifa | código (único por crucero), nombre, descripción, orden |
| `crucero_precio` | (id_salida, id_tarifa, id_cabina_tipo) | precio_pp (> 0) |

Al borrar un crucero se borran en cascada todas sus partes. Sus fotos de `multimedia` las borra la app.

## 3. Fotos

- **Fotos del crucero**: van en `Eurovip.multimedia`, tipo **CRUCERO** (columna `id_crucero`).
  - PORTADA: la tarjeta de la lista.
  - INTERMEDIA: el banner de la cabecera.
  - GALERIA: el slider.
  - Se guardan en la carpeta `{Multimedia:CarpetaFisica}/cruceros/{id}/` con nombre aleatorio `.webp`.
- **Fotos sueltas**: actividad, tipo de cabina, plano de cubierta, día del itinerario, logo de naviera, barco y categoría.
  - Se guardan como URL en su propia columna.
  - El archivo va a `cruceros/{id}/{tipo}/` (o a `cruceros/navieras`, `cruceros/barcos`, `cruceros/categorias`).
  - Se suben con `POST /Cruceros/SubirImagen`, que las convierte a WebP.

## 4. Home (tabla `inicio`)

- Hay **una tarjeta por categoría**, igual que en excursiones y traslados por destino.
- Datos de cada tarjeta:
  - `tipo = 'CRUCERO'`.
  - `id_servicio = id_categoria`.
  - Contenedor **CRUCEROS**.
  - Precio = el «desde» más barato de sus cruceros activos.
  - Imagen = la de la categoría.
- `spActualizaInicioCrucero @idCategoria` crea, actualiza o quita la tarjeta. La quita si la categoría está
  oculta o si no hay ningún crucero activo con fecha futura y precio.
- La app llama a ese SP en estos casos: al guardar, activar, duplicar o eliminar un crucero, y al guardar o
  eliminar una categoría.
- `spSincronizaInicio` también revisa estas tarjetas.
- `vCruceroDesde` da los precios de cada crucero:
  - El precio más bajo en general y el más bajo por categoría de cabina.
  - Solo cuenta salidas futuras y activas.
  - También da la próxima salida.

## 5. Consultas para la web

| SP | Devuelve |
|---|---|
| `spPortalCrucerosCategorias` | categorías visibles con nº de cruceros y precio desde |
| `spPortalListarCruceros @IdCategoria` | tarjetas de la lista (con portada, naviera, logo, itinerario resumido y precios por categoría) |
| `spPortalObtenerCrucero @IdCrucero` | 11 resultados: cabecera, fotos, itinerario, secciones, actividades, salidas, tarifas, tipos de cabina, precios, cubiertas, cabinas |

## 6. Pantalla (EurovipBack → Cruceros)

**Pestaña «Cruceros»**
- Indicadores clicables: total, a la venta, desactivados, sin fechas/precios y sin foto.
- Buscador y filtros por naviera y categoría.
- Se puede ordenar por próxima salida, precio, título o más recientes.
- Cada crucero se muestra en una tarjeta como la de la web. Acciones de la tarjeta:
  - Ver como la web.
  - Editar.
  - Duplicar (la copia queda desactivada).
  - Activar o desactivar.
  - Eliminar.

**Pestaña «Catálogos»**
- Navieras (con logo), barcos (con foto y descripción) y categorías (con foto, orden y visible).
- No se puede borrar una naviera que tiene barcos, ni un barco que tiene cruceros.

**Editor (6 pestañas)**
1. **Datos**: título, subtítulo, barco, código, puertos, noches, activo, categorías y textos.
2. **Fotos**: portada, banner y galería. Se activa después de guardar por primera vez.
3. **Itinerario**:
   - Un día por fila, con «En navegación» y las horas.
   - «Crear los N días» genera las filas según las noches.
4. **A bordo**: secciones con actividades (foto, título, descripción) y el botón «Crear las 4 básicas».
5. **Cabinas**:
   - Cubiertas con su plano.
   - Tipos de cabina por categoría, con su foto, capacidad y m².
   - Cabinas concretas, cada una con su cubierta.
6. **Fechas y precios**:
   - Tarifas.
   - Fechas, de una en una o en serie (desde, hasta y cada N días).
   - Matriz de precios por fecha: tipos de cabina × tarifas.
   - «Copiar estos precios a todas las fechas».

Al guardar, el crucero completo se envía en un solo JSON. El repositorio reemplaza todas las filas hijas en
**una transacción** y las enlaza por las claves que pone la pantalla (`t…`, `s…`, `f…`). Después actualiza
las tarjetas de la Home de sus categorías, las de antes y las de ahora.

Archivos: `Controllers/CrucerosController.cs`, `Repositories/CruceroRepository.cs`,
`Models/CruceroModels.cs`, `Views/Cruceros/Index.cshtml`, `Services/Multimedia.cs` (tipo CRUCERO y
`GuardarArchivoAsync`).

## 7. Para pasar a producción (cuando se pida)

Hay que ejecutar en el servidor que corresponda la FASE 21 (CAMBIO 67 a 77) de `EUROVIP_BaseDeDatos.sql`,
que crea las tablas vacías. Antes de eso hay que tener permiso expreso: hoy está **solo en el 93**.
