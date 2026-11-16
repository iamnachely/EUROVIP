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
            var opciones = hoteles.Select(h => new EuroVip.Services.CargaExcelOpcion(h.Id.ToString(), h.Nombre, h.Destino)).ToList();
            var porId = hoteles.GroupBy(h => h.Id).ToDictionary(g => g.Key, g => g.First());
            // CAMBIO 131: columna opcional «Estado» (Confirmada, Bajo petición…) en la fila de las fechas; se reconoce por parecido
            var colEstado = celdas.FirstOrDefault(c => c.Fila == filaEnc && c.Texto is not null && EuroVip.Services.Parecidos.Compacto(c.Texto) is "estado" or "estados" or "disponibilidad")?.Col;
            var estados = (await EuroVip.Services.EstadosOferta.ListarAsync(cn)).Select(e => new EuroVip.Services.CargaExcelOpcion(e.Id.ToString(), e.Nombre)).ToList();

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
                var item = new ExcelOfertaHotel { Texto = texto, Precios = precios.OrderBy(x => x.Fecha).ToList() };
                // Pedido de la usuaria: «si no reconoces el hotel, busca coincidencias; si son varias, muéstralas todas»
                var (elegida, aproximado, cand) = EuroVip.Services.Coincidencias.Clasificar(texto, opciones);
                if (elegida is not null)
                {
                    var h = porId[int.Parse(elegida.Valor)];
                    item.Estado = "found"; item.Aproximado = aproximado; item.Id_Alojamiento = h.Id; item.Nombre = h.Nombre; item.Destino = h.Destino;
                }
                else if (cand.Count > 0) item.Estado = "multiple";
                item.Candidatos = cand.Select(c => new Opcion { Valor = c.Valor, Texto = c.Texto, Extra = c.Extra }).ToList();
                if (colEstado is int ce && porPos.TryGetValue((fila.Fila, ce), out var celEst) && (celEst.Texto ?? celEst.Numero?.ToString())?.Trim() is { Length: > 0 } est)
                {
                    item.EstadoTexto = est;
                    var (eo, sugEst) = EuroVip.Services.Coincidencias.Elegir(est, estados);
                    if (eo is not null) item.Id_Estado = int.Parse(eo.Valor);
                    else r.Avisos.Add($"«{texto}»: el estado «{est}» no existe" + (sugEst.Count > 0 ? $" (¿{string.Join(" o ", sugEst.Take(3).Select(s => s.Texto))}?)" : "")
                                      + ". Elígelo en el hotel o créalo con «Nuevo estado».");
                }
                r.Hoteles.Add(item);
            }
            if (r.Hoteles.Count == 0) r.Avisos.Add("No encontré filas de hoteles con precios debajo de la fila de fechas.");
            return r;
        }
    }
}
