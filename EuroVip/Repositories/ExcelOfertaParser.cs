using Dapper;
using EuroVip.Models;
using System.Globalization;

namespace EuroVip.Repositories
{
    public interface IExcelOfertaParser
    {
        Task<ExcelOferta> LeerAsync(Stream archivo);
    }

    /// <summary>
    /// Lee el Excel de carga manual igual que BackTiTan (CargarExcel/ProcesarExcel), es una matriz:
    ///   • fila de encabezado (en las filas 1 a 5) = la primera con al menos 2 fechas; columna A es un rótulo;
    ///   • cada fila siguiente: columna A = nombre del hotel, columnas B en adelante = PRECIO TOTAL de esa fecha;
    ///   • se ignoran celdas vacías, "//" o con precio ≤ 0.
    /// Los hoteles se buscan en Eurovip.alojamiento (ES, activos): encontrado / varios / no encontrado.
    /// </summary>
    public class ExcelOfertaParser : IExcelOfertaParser
    {
        private readonly ICircuitoRepository _db;
        public ExcelOfertaParser(ICircuitoRepository db) => _db = db;

        public async Task<ExcelOferta> LeerAsync(Stream archivo)
        {
            var r = new ExcelOferta();
            var celdas = ExcelCircuitoParser.LeerCeldas(archivo);
            var porPos = celdas.ToDictionary(c => (c.Fila, c.Col));

            DateTime? Fecha(ExcelCircuitoParser.Celda c)
            {
                if (ExcelCircuitoParser.EsFecha(c)) return DateTime.FromOADate(c.Numero!.Value).Date;
                if (c.Texto is not null && DateTime.TryParseExact(c.Texto.Trim(), new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd", "dd/MM/yy" },
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d.Date;
                return null;
            }

            // 1) fila de encabezado
            var filaEnc = Enumerable.Range(1, 5).FirstOrDefault(f => celdas.Count(c => c.Fila == f && c.Col >= 2 && Fecha(c) is not null) >= 2);
            if (filaEnc == 0)
            {
                r.Avisos.Add("No encontré la fila de fechas: debe estar en las primeras 5 filas, desde la columna B, con al menos 2 fechas.");
                return r;
            }
            var columnas = celdas.Where(c => c.Fila == filaEnc && c.Col >= 2).Select(c => (c.Col, F: Fecha(c))).Where(x => x.F is not null).OrderBy(x => x.Col).ToList();
            r.Fechas = columnas.Select(x => x.F!.Value).Distinct().OrderBy(x => x).ToList();

            // 2) filas de hoteles
            var filas = celdas.Where(c => c.Col == 1 && c.Fila > filaEnc && c.Texto is { Length: > 1 }).OrderBy(c => c.Fila).ToList();
            using var cn = _db.Conexion();
            var hoteles = (await cn.QueryAsync<(int Id, string Nombre, string Destino)>(
                "SELECT id_alojamiento, nombre, id_destino FROM Eurovip.alojamiento WHERE id_idioma = 'ES' AND activo = 1")).ToList();
            var indice = hoteles.Select(h => (h.Id, h.Nombre, h.Destino, N: ExcelCircuitoParser.Normalizar(h.Nombre))).ToList();

            foreach (var fila in filas)
            {
                var precios = new List<ExcelFecha>();
                foreach (var (col, f) in columnas)
                {
                    if (!porPos.TryGetValue((fila.Fila, col), out var c)) continue;
                    decimal? precio = c.Numero is > 0 ? (decimal)c.Numero.Value
                        : c.Texto is not null && c.Texto.Trim() != "//" && decimal.TryParse(c.Texto.Replace("€", "").Replace("RD$", "").Trim(), NumberStyles.Any, new CultureInfo("es-ES"), out var p) && p > 0 ? p : null;
                    if (precio is not null && !precios.Any(x => x.Fecha == f!.Value))
                        precios.Add(new ExcelFecha { Fecha = f!.Value, Precio = Math.Round(precio.Value, 2) });
                }
                if (precios.Count == 0) continue;   // fila sin precios (rótulos, notas…)

                var texto = fila.Texto!.Trim();
                var n = ExcelCircuitoParser.Normalizar(texto);
                var exactos = indice.Where(h => h.N == n).ToList();
                var cand = exactos.Count > 0 ? exactos : indice.Where(h => h.N.Contains(n) || (n.Length >= 6 && h.N.Length >= 6 && n.Contains(h.N))).Take(15).ToList();
                var item = new ExcelOfertaHotel { Texto = texto, Precios = precios.OrderBy(x => x.Fecha).ToList() };
                if (cand.Count == 1) { item.Estado = "found"; item.Id_Alojamiento = cand[0].Id; item.Nombre = cand[0].Nombre; item.Destino = cand[0].Destino; }
                else if (cand.Count > 1) { item.Estado = "multiple"; item.Candidatos = cand.Select(h => new Opcion { Valor = h.Id.ToString(), Texto = h.Nombre, Extra = h.Destino }).ToList(); }
                r.Hoteles.Add(item);
            }
            if (r.Hoteles.Count == 0) r.Avisos.Add("No encontré filas de hoteles con precios debajo de la fila de fechas.");
            return r;
        }
    }
}
