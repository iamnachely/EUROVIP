# Cruceros: tasas, precio base y precio web

> Para: desarrollador de la web (repo EuroVip: `web/` y `api/`).
> De: BackOffice EUROVIP (`bktitan.eurovip.com.do/Cruceros/Index`, modal de carga y de edición).
> Fecha: 20/11/2026 · Base de datos: **TropitoursVDT**, esquema `Eurovip`.

## 1. Qué cambió, en una frase

En el BackOffice, el precio que se escribe por fecha y tipo de cabina ahora es el **precio base** por persona, y el crucero tiene un campo
nuevo de **tasas por persona**. El **precio web** es **`precio base + tasas`**. La web tiene que poder mostrar los tres: **tasas**,
**precio base** y **precio web**.

**No cambió nada de lo que la web ya lee:** `Eurovip.crucero_precio.precio_pp` sigue siendo el **precio que se vende** (el precio web),
ya con las tasas sumadas. Los cruceros de antes no cambian: su `tasas` es `NULL` (se lee como 0) y su precio base es igual a su precio web.

## 2. Qué hay en la base de datos

Una columna nueva, opcional:

```sql
ALTER TABLE Eurovip.crucero ADD tasas decimal(12, 2) NULL;   -- Database/cruceros-tasas.sql
```

| Dato | Dónde está | Cómo se calcula |
|---|---|---|
| **Tasas por persona** | `Eurovip.crucero.tasas` (nueva) | una por crucero, igual para todas las fechas y cabinas. `NULL` = sin tasas |
| **Precio web** | `Eurovip.crucero_precio.precio_pp` (la de siempre) | `precio base + tasas` |
| **Precio base** | **no se guarda**, se calcula | `precio_pp - ISNULL(crucero.tasas, 0)` |

> Todo es **por persona** (base 2 personas compartiendo), en la moneda del crucero (`crucero.id_moneda`), como hasta ahora.

Ejemplo: tasas `RD$ 4,500` y precio base de la cabina interior `RD$ 54,870` → `precio_pp` guardado = `59,370` (precio web).

## 3. Cómo leerlo (SQL de ejemplo)

```sql
SELECT  c.id_crucero,
        s.fecha_salida,
        t.codigo                          AS tarifa,
        ct.categoria, ct.nombre           AS cabina,
        p.precio_pp                       AS precio_web,
        ISNULL(c.tasas, 0)                AS tasas,
        p.precio_pp - ISNULL(c.tasas, 0)  AS precio_base,
        c.id_moneda
FROM    Eurovip.crucero c
JOIN    Eurovip.crucero_salida      s  ON s.id_crucero = c.id_crucero AND s.activo = 1
JOIN    Eurovip.crucero_precio      p  ON p.id_salida = s.id_salida
JOIN    Eurovip.crucero_tarifa      t  ON t.id_tarifa = p.id_tarifa
JOIN    Eurovip.crucero_cabina_tipo ct ON ct.id_cabina_tipo = p.id_cabina_tipo
WHERE   c.id_crucero = @id AND c.activo = 1;
```

Con la columna `tasas` en la misma fila del crucero, **no hace falta tocar** las consultas que ya calculan el «desde» ni los
procedimientos de la Home (`precio_pp` es el precio web de siempre).

## 4. Qué tiene que hacer la web

1. **API (`api/`)**: al leer un crucero, devolver también `tasas` (número, `0` si es `NULL`) y, por cada precio, `precioBase` y `precioWeb`
   (`precioWeb` = `precio_pp`; `precioBase` = `precio_pp - tasas`).
2. **Ficha del crucero (`web/`)**: junto al precio de cada fecha y cabina, mostrar:
   - **Precio web** (lo que paga la persona): el número grande de siempre.
   - Un desglose opcional: **Precio base** + **Tasas** = **Precio web** (por ejemplo, en un texto pequeño o un tooltip «Incluye RD$ 4,500
     de tasas»).
3. **El «desde» por persona de la tarjeta y de la Home** sigue siendo el precio web más bajo (`MIN(precio_pp)`): no cambia.
4. **Fórmula siempre igual:** `precio_web = precio_base + tasas`. No recalcular el web a partir del base en la web: **el web ya viene
   guardado en `precio_pp`**; el base y las tasas son para mostrar el desglose.
5. Si `tasas` es `NULL` o `0`, **no mostrar el desglose** (el precio base y el web son lo mismo).

## 5. Cómo se ve en el BackOffice (para que la web use las mismas palabras)

En el paso **7 · Fechas y precios** del crucero (modal de carga y de edición):

- Cuadro **«Tasas por persona»** con la fórmula **Precio base + Tasas = Precio web**.
- La tabla de fechas × tipos de cabina dice **«Precio base por persona»**; debajo de cada precio escrito aparece
  **«Web RD$ …»** con la suma.
- Si se cambian las tasas, el precio base de cada celda se queda igual y el precio web se mueve la diferencia.

## 6. Orden de despliegue (importante)

1. Aplicar `Database/cruceros-tasas.sql` en TropitoursVDT (agrega la columna; es opcional y no cambia nada existente).
2. Desplegar el BackOffice. **Mientras la columna no exista**, el BackOffice **oculta** el campo de tasas y todo funciona como antes.
3. Desplegar la web con el desglose.

## 7. Qué NO cambió

- Ninguna tabla ni procedimiento existente; `crucero_precio.precio_pp` sigue siendo `money`, mayor que 0, por persona.
- Las cargas de cruceros de **proveedores** y por **Excel** siguen guardando su precio directamente como precio web (sin tasas):
  para esos cruceros `tasas` es `NULL` y el precio base es igual al web.
