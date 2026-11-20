-- Cruceros: tasas por persona (20/11/2026, pedido de la usuaria).
-- En el modal de carga y edición de Cruceros el precio que se escribe es el PRECIO BASE por persona; el PRECIO WEB es
-- precio base + tasas. Eurovip.crucero_precio.precio_pp sigue guardando el precio web (la web y la Home no cambian);
-- aquí se guarda la tasa por persona para poder volver a mostrar el precio base al editar (base = precio_pp - tasas).
-- Columna opcional: los cruceros de antes quedan con tasas = NULL (se leen como 0, su precio no cambia).
-- Aplicar en TropitoursVDT ANTES de usar las tasas. Mientras no exista la columna, la pantalla oculta el campo de tasas y todo
-- funciona como siempre.
IF COL_LENGTH('Eurovip.crucero', 'tasas') IS NULL
    ALTER TABLE Eurovip.crucero ADD tasas decimal(12, 2) NULL;
GO
