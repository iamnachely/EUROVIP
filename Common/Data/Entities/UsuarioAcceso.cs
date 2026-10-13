namespace Common.Data.Entities;

/// <summary>Tabla [TropitoursVDT].[Eurovip].[UsuarioAcceso] — catálogo de módulos del sistema.</summary>
public class UsuarioAcceso
{
    public int Id_Modulo { get; set; }
    public string Nombre_Modulo { get; set; } = null!;
    public string? Categoria { get; set; }
}

/// <summary>Tabla [TropitoursVDT].[Eurovip].[UsuarioModulos] — permiso de un usuario (userName) sobre un módulo.</summary>
public class UsuarioModulos
{
    public int Id { get; set; }
    public string Usuario { get; set; } = null!;
    public bool Acceso { get; set; }
    public int Id_Modulo { get; set; }
}

/// <summary>Tabla [TropitoursVDT].[Eurovip].[rol].</summary>
public class Rol
{
    public string? CodigoRol { get; set; }
}
