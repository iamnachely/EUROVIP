namespace Common.Data.Entities;

/// <summary>
/// Tabla [TropitoursVDT].[Eurovip].[inicio] — vitrina de la web (réplica de ViajeVDT.dbo.inicio).
/// En la carga de hoteles: tipo_servicio = 'HOTEL' e id_destino = nombre del hotel.
/// </summary>
public class Inicio
{
    public int Id_Servicio { get; set; }
    public string Id_Idioma { get; set; } = null!;
    public string Tipo_Servicio { get; set; } = null!;
    public short Id_Campanya { get; set; }
    public string Id_Moneda { get; set; } = null!;
    public string Id_Pais_Origen { get; set; } = null!;
    public string Origen { get; set; } = null!;
    public string Id_PaisDestino { get; set; } = null!;
    public string Id_Destino { get; set; } = null!;
    public string? CaratulaDestino { get; set; }
    public string Descripcion { get; set; } = null!;
    public string? Latitud { get; set; }
    public string? Longitud { get; set; }
    public decimal? PrecioWeb { get; set; }
    public int? Id_Contenedor { get; set; }
    public string? Contenedor { get; set; }
    public int? Posicion_Contenedor { get; set; }
    public int? Posicion_Destino { get; set; }
    public bool? Estado { get; set; }
    public string? Notas { get; set; }
    public int? Noches { get; set; }
}

/// <summary>[Eurovip].[contenedores] — secciones de la web.</summary>
public class Contenedor
{
    public int Id_Contenedor { get; set; }
    public string Nombre { get; set; } = null!;
    public int Posicion { get; set; }
    public bool Estado { get; set; }
    public string? Id_Idioma { get; set; }
}

/// <summary>[Eurovip].[campanya].</summary>
public class Campanya
{
    public short Id_Campanya { get; set; }
    public string Id_Idioma { get; set; } = null!;
    public string Id_Tipo_Servicio { get; set; } = null!;
    public string Nombre { get; set; } = null!;
}

/// <summary>[Eurovip].[idioma] / [Eurovip].[moneda] — catálogos código + nombre.</summary>
public class CodigoNombre
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
}
