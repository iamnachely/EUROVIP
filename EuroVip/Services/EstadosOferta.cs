using System.Data;
using Dapper;

namespace EuroVip.Services
{
    /// <summary>
    /// Estado de las ofertas de hotel (CAMBIO 131): Confirmada, Bajo petición… (catálogo Eurovip.estado_oferta) y la columna
    /// opcional servicio.id_estado_oferta. Pedido de la usuaria: «que se pueda poner estos estados, con un botón para que el usuario
    /// agregue otros; también en el Excel; siempre opcional y editable».
    /// </summary>
    public static class EstadosOferta
    {
        public sealed record Estado(int Id, string Nombre);

        public static async Task<List<Estado>> ListarAsync(IDbConnection cn, IDbTransaction? tx = null) =>
            (await cn.QueryAsync<Estado>("SELECT id_estado_oferta AS Id, nombre AS Nombre FROM Eurovip.estado_oferta WHERE activo = 1 ORDER BY orden, nombre",
                transaction: tx)).ToList();

        /// <summary>Crea un estado (o devuelve el que ya existe igual o casi igual: «bajo peticion» = «Bajo petición»).</summary>
        public static async Task<(Estado? Estado, bool Nuevo, string? Error)> CrearAsync(IDbConnection cn, string? nombre, string usuario)
        {
            var n = ReglasDatos.Limpiar(nombre);
            if (n.Length < 3) return (null, false, "Escribe el nombre del estado (al menos 3 letras).");
            if (n.Length > 60) return (null, false, "El nombre del estado admite como máximo 60 caracteres.");
            if (!n.Any(char.IsLetter)) return (null, false, "El nombre del estado tiene que llevar letras.");
            var lista = await ListarAsync(cn);
            var igual = lista.FirstOrDefault(e => Parecidos.Compacto(e.Nombre) == Parecidos.Compacto(n));
            if (igual is not null) return (igual, false, null);
            var id = await cn.ExecuteScalarAsync<int>(@"INSERT INTO Eurovip.estado_oferta (nombre, orden, usuario)
                VALUES (@n, (SELECT ISNULL(MAX(orden), 0) + 1 FROM Eurovip.estado_oferta), @usuario); SELECT CAST(SCOPE_IDENTITY() AS int);",
                new { n, usuario = usuario.Length > 50 ? usuario[..50] : usuario });
            return (new Estado(id, n), true, null);
        }

        /// <summary>Error si el estado elegido no existe (null si está vacío o existe).</summary>
        public static async Task<string?> ErrorAsync(IDbConnection cn, int? id, IDbTransaction? tx = null) =>
            id is null || await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Eurovip.estado_oferta WHERE id_estado_oferta = @id", new { id }, tx) > 0
                ? null : "Ese estado ya no existe. Elige otro de la lista.";
    }
}
