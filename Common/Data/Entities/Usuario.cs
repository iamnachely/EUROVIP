using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Common.Data.Entities;

/// <summary>Tabla [TropitoursVDT].[Eurovip].[usuario] — réplica de ViajeVDT.dbo.usuario (10.10.10.29).</summary>
public class Usuario
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("codigoUsuario")]
    public int CodigoUsuario { get; set; }

    [Column("nombre"), StringLength(50)]
    public string Nombre { get; set; } = null!;

    [Column("apellido"), StringLength(50)]
    public string Apellido { get; set; } = null!;

    [Column("telefono"), StringLength(15)]
    public string? Telefono { get; set; }

    [Column("clave")]
    public string Clave { get; set; } = null!;

    [Column("imagen"), StringLength(100)]
    public string? Imagen { get; set; }

    [Column("activo")]
    public bool Activo { get; set; }

    [Column("rol"), StringLength(30)]
    public string? Rol { get; set; }

    [Column("email"), StringLength(50)]
    public string Email { get; set; } = null!;

    [Column("userName"), StringLength(15)]
    public string? UserName { get; set; }

    public bool? Auth2FAValidator { get; set; }
    public int? Auth2FACode { get; set; }
    public string? SecurityStamp { get; set; }

    [Column("correoAD"), StringLength(70)]
    public string? CorreoAD { get; set; }
}
