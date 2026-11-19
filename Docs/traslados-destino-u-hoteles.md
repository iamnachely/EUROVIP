# Traslados: «Llega a» un destino O uno o varios hoteles

> Para: desarrollador de la web (repo EuroVip: `web/` y `api/`).
> De: BackOffice EUROVIP (`bktitan.eurovip.com.do/Traslados/Index`).
> Fecha: 20/11/2026 · Base de datos: **TropitoursVDT**, esquema `Eurovip` (no cambió ninguna tabla ni procedimiento).

## 1. Qué cambió, en una frase

Un traslado ya no llega siempre «a un destino»: ahora **llega a UN destino (sirve para todos sus hoteles) O a uno o varios hoteles concretos**.
Siempre es una de las dos y **nunca las dos ni ninguna**. La web tiene que entender los dos casos.

Hoy la web **solo muestra los traslados de «todos los hoteles»** (`ListarTraslados.Leer` filtra `TodosHoteles`), así que los traslados que el
BackOffice cargue **a un hotel** no se ven en la web hasta que se implemente lo de la sección 5.

## 2. Reglas de negocio

| Modo en el BackOffice | `traslado.todos_hoteles` | `traslado.id_destino` | Filas en `traslado_alojamiento` | Quién lo ve |
|---|---|---|---|---|
| **Un destino** | `1` | el destino elegido (ej. `Punta Cana`) | ninguna | todos los hoteles de ese destino |
| **Uno o varios hoteles** | `0` | el destino **del hotel** (lo calcula el BackOffice) | una por hotel (`id_alojamiento`) | solo esos hoteles |

- Con hoteles, **todos deben ser del mismo destino** (si no, el BackOffice no deja guardar). Por eso `id_destino` siempre está lleno y coincide
  con el destino de sus hoteles.
- El aeropuerto de salida sigue en `id_estacion_origen` (ej. `PUJ`). El destino del aeropuerto (`estacion.id_destino`) **no** se usa como destino
  del traslado: el destino sale del destino elegido o del hotel.
- Mensajes de error del BackOffice (para que la web use el mismo criterio si algún día valida):
  - «Elige el destino o, si prefieres, uno o varios hoteles.» (modo destino vacío)
  - «Elige el hotel o los hoteles a los que llega, o elige un destino.» (modo hotel sin hoteles)
  - «Elige una sola opción: un destino (todos sus hoteles) o uno o varios hoteles, no las dos.»
  - «Los hoteles tienen que ser del mismo destino (hay de X y Y). Haz un traslado por cada destino.»

## 3. Datos: ejemplos

```sql
-- A) Traslado a TODO un destino
-- traslado: id_traslado=7, id_estacion_origen='PUJ', id_destino='Punta Cana', todos_hoteles=1
-- traslado_alojamiento: (sin filas)

-- B) Traslado a DOS hoteles de Punta Cana
-- traslado: id_traslado=9, id_estacion_origen='PUJ', id_destino='Punta Cana', todos_hoteles=0
-- traslado_alojamiento: (9, 1234), (9, 1240)
```

Para saber a qué hoteles llega un traslado de tipo B:

```sql
SELECT ta.id_alojamiento, LTRIM(RTRIM(a.nombre)) AS hotel
FROM Eurovip.traslado_alojamiento ta
JOIN Eurovip.alojamiento a ON a.id_alojamiento = ta.id_alojamiento
WHERE ta.id_traslado = @IdTraslado;
```

Los traslados que ya existían quedaron como estaban: casi todos son tipo A (`todos_hoteles = 1`).

## 4. Cómo se cargan ahora (BackOffice)

- **Formulario** (crear y editar): en «Ruta», `Desde (aeropuerto)` y el selector **«Llega a»** con dos botones: *Un destino* (todos sus hoteles) o *Uno o varios hoteles*.
  Solo se guarda la opción activa. Al crear empieza en hoteles; al editar, en lo que tenga el traslado.
- **Cargar Excel**: columnas `Aeropuerto`, `Destino` y `Hotel`. Se llena **una sola** de `Destino` u `Hotel` por fila (si vienen las dos o ninguna, la fila
  sale con error). Una fila = un hotel; para varios hoteles se repite el traslado en varias filas. El hotel se escribe con el nombre de Eurovip,
  o `Nombre · Destino` si hay dos hoteles con el mismo nombre.
- **Cargas de proveedores** (Gestión → Proveedor): **no cambiaron**; siguen cargando por destino (`todos_hoteles = 1`).
- Duplicados: un traslado se considera repetido si coincide aeropuerto, destino, tipo, categoría, vehículo y nombre, y (en modo hotel) comparte algún hotel.

## 5. Qué debe implementar la web (EuroVip)

### 5.1 Procedimiento `Eurovip.spPortalListarTraslados` (`db/tropitoursvdt/procedimientos/`)

Hoy devuelve `todos_hoteles` pero **no los hoteles** de los traslados tipo B. Hay que agregar los hoteles. Propuesta (la más simple, sin romper el
contrato actual): un **segundo resultado** con las parejas traslado–hotel.

```sql
-- al final de spPortalListarTraslados, después del SELECT actual:
SELECT ta.id_traslado, ta.id_alojamiento, LTRIM(RTRIM(a.nombre)) AS hotel
FROM Eurovip.traslado_alojamiento ta
JOIN Eurovip.traslado t ON t.id_traslado = ta.id_traslado AND t.activo = 1
JOIN Eurovip.alojamiento a ON a.id_alojamiento = ta.id_alojamiento
WHERE (@Destino IS NULL OR t.id_destino = LTRIM(RTRIM(@Destino)));
```

(Alternativa: un parámetro `@IdAlojamiento` como el de `spPortalBuscarTraslados`.)

### 5.2 API `api/EuroVip.Application/Catalogo/ListarTraslados.cs` y `CatalogoRepositorio.cs`

- Quitar el filtro `.Where(f => f.TodosHoteles)` de `ListarTraslados.Leer` (hoy deja fuera los de hotel concreto).
- Agregar a `Traslado` (el record público): `bool TodosHoteles` y `IReadOnlyList<HotelTraslado> Hoteles` (`HotelTraslado(int IdAlojamiento, string Nombre)`),
  vacío cuando `TodosHoteles` es true.
- `CatalogoRepositorio.ListarTraslados` debe leer el segundo resultado (`QueryMultiple`) y unir los hoteles a cada traslado.
- Aceptar un filtro por hotel (opcional): `GET /catalogo/traslados?idAlojamiento=…`.

### 5.3 Pantallas públicas

| Pantalla | Qué mostrar |
|---|---|
| `traslados/[destino]` (lista) | Todos los traslados del destino. Tipo A: «Llega a todos los hoteles de {destino}». Tipo B: «Llega a: {hotel 1}, {hotel 2}» (o «a N hoteles»). |
| `traslados/[destino]/[traslado]` (ficha) | Igual, con la lista completa de hoteles si es tipo B. |
| `hotel/[destino]/[hotel]` (ficha del hotel, `TrasladosHotel`) | Los traslados del destino que sean **tipo A** **más** los **tipo B que incluyan ese hotel**. |
| Reserva de un traslado | Guardar el `id_traslado` y, si el cliente viene de un hotel, el hotel; en tipo B, **validar** que el hotel pertenezca al traslado. |
| Tarjeta de la Home por destino | Sin cambios (`inicio` sigue agrupando por `id_destino`). |

### 5.4 Búsqueda con `Eurovip.spPortalBuscarTraslados`

**No hay que cambiarlo**: ya contempla los dos modos.

```sql
AND (t.todos_hoteles = 1 OR @IdAlojamiento IS NULL
     OR EXISTS (SELECT 1 FROM Eurovip.traslado_alojamiento a WHERE a.id_traslado = t.id_traslado AND a.id_alojamiento = @IdAlojamiento))
```

- Con `@IdAlojamiento`: devuelve los tipo A del destino del hotel **y** los tipo B de ese hotel.
- Con solo `@Destino` (sin hotel): devuelve **todos** los del destino, incluidos los tipo B. Decidir si la web los muestra tal cual (recomendado, con el
  texto «Llega a: …») o si en la búsqueda por destino filtra solo tipo A.

## 6. Casos a probar en la web

1. Traslado tipo A (`PUJ` → destino `Punta Cana`): aparece en la lista del destino y en la ficha de **cualquier** hotel de Punta Cana.
2. Traslado tipo B con 2 hoteles: aparece en la lista del destino con sus hoteles, y **solo** en la ficha de esos 2 hoteles; en otro hotel del mismo destino **no**.
3. Traslado tipo B y reserva desde un hotel que no es suyo: debe rechazarse.
4. Traslado inactivo (`activo = 0`): no aparece en ninguna parte.
5. Búsqueda `PUJ` + hotel del traslado B: trae A + B. Búsqueda `PUJ` + otro hotel del mismo destino: solo A.
6. Un hotel que se elimine o desactive del catálogo: el traslado B no debe romper la lista (mostrar el resto de hoteles).

## 7. Dónde está el código del BackOffice (referencia)

| Qué | Archivo (repo BackEurovip) |
|---|---|
| Formulario y selector «Llega a» | `EuroVip/Views/Traslados/Index.cshtml` |
| Reglas de guardado (destino o hoteles) | `EuroVip/Services/GuardadoProductos.cs` → `RevisarTrasladoAsync` |
| Excel (plantilla y carga) | `EuroVip/Controllers/TrasladosController.cs` (`ColumnasExcelAsync`, `Plantilla`, `CargarExcel`) y `EuroVip/Services/HotelesExcel.cs` |
| Duplicados y búsqueda de hoteles | `EuroVip/Repositories/TrasladoRepository.cs` |
| Definición de las tablas y del SP de búsqueda | `Database/EUROVIP_BaseDeDatos.sql` (CAMBIO 53 y 55) |

Commits: `0e621b3` (aeropuerto a hotel), `952b07e` (arreglo del formulario), `5946de1` («Llega a» = destino o hoteles), `51d3339` (título de la sección 8).
Tickets EurovipBack: #4449, #4451, #4453, #4455.
