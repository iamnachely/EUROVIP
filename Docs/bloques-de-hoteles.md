# Bloques de hoteles (cupo de habitaciones) — guía para la web

> CAMBIO 132 de `Database/EUROVIP_BaseDeDatos.sql` · aplicado en el servidor 93 (`TropitoursVDT`, esquema `Eurovip`) · 17/11/2026

## Qué es

Al cargar una oferta de **Solo Hotel** o **Paquetes** (BackOffice: `/CargaManual/Express`, `/CargaManual/Clasica`, Excel y el
editor de `/Ofertas/Index`), el usuario puede poner, **por cada habitación marcada**, cuántas habitaciones de ese tipo tiene la
empresa para vender **en esa fecha**. Es el «bloque» o cupo.

- Es **opcional**. Si está vacío, la habitación se vende **sin límite** (como hasta ahora).
- Es **por oferta (fecha) y habitación**: cada fila de `Eurovip.servicio_habitacion` tiene su propio cupo.
- La web **no** cambia el cupo: solo **suma vendidas** al reservar y **las resta** al cancelar, con los procedimientos de abajo.

## Dónde está

### Tabla `Eurovip.servicio_habitacion` (columnas nuevas)

| Columna | Tipo | Qué significa |
|---|---|---|
| `cupo` | `int NULL` | Habitaciones para vender en esa oferta. `NULL` = sin límite. `0` = no se vende (agotada). |
| `cupo_vendido` | `int NOT NULL DEFAULT 0` | Habitaciones ya vendidas. Lo mueve la web con los procedimientos. El BackOffice lo conserva siempre. |

Clave de cada fila: `id_servicio` (la oferta / fecha) + `id_habitacion` (con `id_alojamiento` e `id_idioma = 'ES'`).

### Vista `Eurovip.vInventarioHabitacion` (para leer)

| Columna | Qué es |
|---|---|
| `id_servicio`, `id_tipo_servicio` (`OFERTA` / `PAQUETES`), `activo` | La oferta |
| `desde`, `hasta` | Fechas de la estancia (de `servicio_detalle`, secuencia 1) |
| `id_alojamiento`, `hotel`, `id_habitacion`, `habitacion` | Hotel y habitación |
| `cupo`, `cupo_vendido` | Lo de la tabla |
| `disponibles` | `cupo - cupo_vendido` (nunca negativo). **`NULL` = sin límite** |
| `agotada` | `1` si tiene cupo y ya no queda ninguna |

```sql
-- Qué queda de cada habitación de una oferta
SELECT id_habitacion, habitacion, cupo, cupo_vendido, disponibles, agotada
FROM Eurovip.vInventarioHabitacion
WHERE id_servicio = @id_servicio;
```

## Cómo usarlo en la web

### 1. Mostrar disponibilidad

- `disponibles IS NULL` → sin límite: no mostrar nada (o «Disponible»).
- `disponibles > 0` → se puede reservar. Si es poco (p. ej. ≤ 3), conviene un aviso «¡Quedan 2 habitaciones!».
- `agotada = 1` → **no** dejar elegir esa habitación (mostrar «Agotada»). Si todas las habitaciones de la oferta están
  agotadas, la oferta no se puede reservar.
- Al pedir más habitaciones de las que quedan, avisar antes de enviar: «Solo quedan N habitaciones de este tipo».

### 2. Reservar (descontar del cupo)

Usar **siempre** el procedimiento: es atómico y no deja vender de más aunque dos personas reserven a la vez.

```sql
DECLARE @ok bit, @disponibles int;
EXEC Eurovip.spInventarioReservar
     @id_servicio = 1234, @id_habitacion = 56, @cantidad = 2,
     @ok = @ok OUTPUT, @disponibles = @disponibles OUTPUT;
-- @ok = 1 → reservado (si la habitación no tiene límite, siempre 1)
-- @ok = 0 → no quedan suficientes: NO confirmar la reserva; mostrar «Solo quedan @disponibles»
```

Ejemplo con Dapper:

```csharp
var p = new DynamicParameters(new { id_servicio = idServicio, id_habitacion = idHabitacion, cantidad = habitaciones });
p.Add("ok", dbType: DbType.Boolean, direction: ParameterDirection.Output);
p.Add("disponibles", dbType: DbType.Int32, direction: ParameterDirection.Output);
await cn.ExecuteAsync("Eurovip.spInventarioReservar", p, tx, commandType: CommandType.StoredProcedure);
if (!p.Get<bool>("ok")) return Conflicto($"Solo quedan {p.Get<int?>("disponibles") ?? 0} habitaciones de este tipo.");
```

- Llamarlo **dentro de la misma transacción** que crea la reserva: si la reserva falla, se hace rollback y el cupo vuelve solo.
- Llamarlo una vez **por habitación** reservada (`@cantidad` = cuántas de ese tipo).

### 3. Cancelar (devolver al cupo)

```sql
EXEC Eurovip.spInventarioLiberar @id_servicio = 1234, @id_habitacion = 56, @cantidad = 2;
```

Nunca deja `cupo_vendido` por debajo de 0.

## Lo que hace el BackOffice (para que la web sepa qué esperar)

- Carga Express y Clásica: el cupo de cada habitación se copia a **todas las fechas** que se crean en esa carga.
- Excel de Solo Hotel/Paquetes: columna opcional **«Cupo»** (columna C de la plantilla).
- Editor de una oferta (`/Ofertas/Index`): se puede cambiar el cupo de cada habitación. Muestra «N vendidas · quedan M».
  - **No** deja poner un cupo menor que lo vendido.
  - **No** deja quitar una habitación que ya tiene ventas.
  - Al guardar, `cupo_vendido` **se conserva** (aunque las habitaciones se borren y se vuelvan a crear internamente).
- Lista de `/Ofertas`: etiqueta «quedan X de Y» o «Agotada» en las ofertas que tienen cupo.

## Lo que NO cambia

- Las ofertas existentes quedaron con `cupo = NULL` (sin límite) y `cupo_vendido = 0`: la web se comporta igual que antes
  hasta que alguien cargue un cupo.
- Precio, suplementos (`servicio_habitacion.precio`) y la forma de buscar ofertas no cambian.

## Deshacer

```sql
DROP PROCEDURE Eurovip.spInventarioReservar; DROP PROCEDURE Eurovip.spInventarioLiberar; DROP VIEW Eurovip.vInventarioHabitacion;
ALTER TABLE Eurovip.servicio_habitacion DROP CONSTRAINT CK_Eurovip_servicio_habitacion_cupo, CK_Eurovip_servicio_habitacion_vendido,
      DF_Eurovip_servicio_habitacion_vendido;
ALTER TABLE Eurovip.servicio_habitacion DROP COLUMN cupo, cupo_vendido;
```
