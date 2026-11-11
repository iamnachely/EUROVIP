namespace EuroVip.Services
{
    /// <summary>
    /// Busca a qué opción de la base se refiere lo que escribió la usuaria en un Excel (destino, hotel, proveedor, moneda…).
    /// Usa las reglas de <see cref="Parecidos"/> (sin tildes, mayúsculas, espacios ni palabras de relleno; letras cambiadas)
    /// y además mira si todas las palabras escritas están en la opción («riu bavaro» → «Riu Palace Bávaro»).
    /// </summary>
    public static class Coincidencias
    {
        /// <summary>Puntos de 0 a 1 de lo escrito contra una opción (mira el valor, el texto y el extra). 0 = no se parece.</summary>
        public static double Puntos(string escrito, CargaExcelOpcion o)
        {
            double mejor = 0;
            // El Extra (país, destino, código) solo cuenta si es igual (Igual); para parecidos se mira el valor y el texto
            foreach (var t in new[] { o.Valor, o.Texto }.Distinct())
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                var corto = Parecidos.Compacto(t).Length <= 3;
                if (Parecidos.Comparar(escrito, t, soloExacto: corto) is { } c) mejor = Math.Max(mejor, c.Puntos);
                // Error de tipeo (1 letra distinta; 2 si es largo): «punta kana» → «Punta Cana»
                var (a, b) = (Parecidos.Compacto(escrito), Parecidos.Compacto(t));
                if (a.Length >= 5 && Math.Abs(a.Length - b.Length) <= 2 && Parecidos.Distancia(a, b) <= (a.Length >= 10 ? 2 : 1)) mejor = Math.Max(mejor, .92);
                // Todas las palabras escritas (de 3+ letras) están en la opción, en cualquier orden
                var palabras = Parecidos.Normal(escrito).Split(' ').Where(p => p.Length >= 3).ToList();
                var opcion = Parecidos.Normal(t);
                if (palabras.Count > 0 && palabras.All(p => opcion.Contains(p, StringComparison.Ordinal)))
                    mejor = Math.Max(mejor, .7 + .25 * Math.Min(1, Parecidos.Compacto(escrito).Length / (double)Math.Max(1, Parecidos.Compacto(t).Length)));
            }
            return mejor;
        }

        public static bool Igual(string escrito, CargaExcelOpcion o) =>
            Parecidos.Compacto(escrito) is var e && e != "" && (e == Parecidos.Compacto(o.Valor) || e == Parecidos.Compacto(o.Texto) || e == Parecidos.Compacto(o.Extra));

        /// <summary>Las opciones que se parecen, de la más a la menos parecida.</summary>
        public static List<(CargaExcelOpcion Opcion, double Puntos)> Buscar(string escrito, IEnumerable<CargaExcelOpcion> opciones, int max = 12) =>
            opciones.Select(o => (o, p: Igual(escrito, o) ? 1.0 : Puntos(escrito, o))).Where(x => x.p > 0)
                    .OrderByDescending(x => x.p).ThenBy(x => x.o.Texto.Length).Take(max).ToList();

        /// <summary>
        /// Para los Excel de hoteles (Solo Hotel, Paquetes, Circuitos): la opción si se reconoce (Aproximado = por parecido, hay que
        /// revisarla) y si no, todas las coincidencias para elegir (vacío = no se parece a nada: se busca a mano).
        /// Solo se compara contra Valor y Texto (no contra Extra, que es el destino que se muestra).
        /// </summary>
        public static (CargaExcelOpcion? Elegida, bool Aproximado, List<CargaExcelOpcion> Candidatos) Clasificar(string escrito, IReadOnlyList<CargaExcelOpcion> opciones)
        {
            var e = Parecidos.Compacto(escrito);
            var iguales = opciones.Where(o => e != "" && Parecidos.Compacto(o.Texto) == e).ToList();
            if (iguales.Count == 1) return (iguales[0], false, new());
            if (iguales.Count > 1) return (null, false, iguales.Take(15).ToList());
            var lista = opciones.Select(o => (o, p: Puntos(escrito, o with { Extra = null }))).Where(x => x.p > 0)
                                .OrderByDescending(x => x.p).ThenBy(x => x.o.Texto.Length).Take(15).ToList();
            if (lista.Count == 0) return (null, false, new());
            var segundo = lista.Count > 1 ? lista[1].p : 0;
            if (lista[0].p >= .85 && (lista.Count == 1 || lista[0].p - segundo >= .15)) return (lista[0].o, true, lista.Skip(1).Take(5).Select(x => x.o).ToList());
            return (null, false, lista.Select(x => x.o).ToList());
        }

        /// <summary>
        /// La opción si se reconoce sin dudas (igual, o un solo parecido claro); si no, null y las coincidencias para elegir
        /// (vacío si no se parece a nada: la pantalla deja buscar en toda la lista).
        /// </summary>
        public static (CargaExcelOpcion? Elegida, List<CargaExcelOpcion> Sugerencias) Elegir(string escrito, IReadOnlyList<CargaExcelOpcion> opciones)
        {
            var iguales = opciones.Where(o => Igual(escrito, o)).ToList();
            if (iguales.Count >= 1 && iguales.Select(o => o.Valor).Distinct().Count() == 1) return (iguales[0], new());
            if (iguales.Count > 1) return (null, iguales.Take(12).ToList());
            var lista = Buscar(escrito, opciones);
            if (lista.Count == 0) return (null, new());
            // Un solo parecido, o uno muy claro por encima del resto → se elige solo (y se avisa para que lo revise)
            var segundo = lista.Count > 1 ? lista[1].Puntos : 0;
            if (lista[0].Puntos >= .8 && (lista.Count == 1 || lista[0].Puntos - segundo >= .15)) return (lista[0].Opcion, new());
            return (null, lista.Select(x => x.Opcion).ToList());
        }
    }
}
