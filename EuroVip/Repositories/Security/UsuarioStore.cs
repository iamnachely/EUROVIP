using Common.Data.Entities;
using Dapper;
using EuroVip.Helpers;
using Microsoft.AspNetCore.Identity;
using System.Data.SqlClient;

namespace EuroVip.Repositories.Security
{
    /// <summary>
    /// UserStore de Identity sobre Dapper (patrón BackTiTan). Login por email;
    /// el hash de contraseña vive en Eurovip.usuario.clave (PasswordHasher estándar de Identity).
    /// </summary>
    public class UsuarioStore : IUserStore<Usuario>, IUserSecurityStampStore<Usuario>, IUserEmailStore<Usuario>, IUserPasswordStore<Usuario>
    {
        private readonly IRepository<Usuario> _repositorioUsuario;
        private readonly IConfiguration _configuration;

        public UsuarioStore(IRepository<Usuario> repositorioUsuario, IConfiguration configuration)
        {
            _repositorioUsuario = repositorioUsuario;
            _configuration = configuration;
        }

        private SqlConnection Conexion() => new(_configuration.GetConnectionString("DefaultConnection"));

        public async Task<IdentityResult> CreateAsync(Usuario user, CancellationToken cancellationToken)
        {
            using var cn = Conexion();
            await cn.ExecuteAsync(@"INSERT INTO Eurovip.usuario (nombre, apellido, telefono, clave, imagen, activo, rol, email, userName, Auth2FACode, SecurityStamp)
                                    VALUES (@Nombre, @Apellido, @Telefono, @Clave, @Imagen, 1, @Rol, @Email, @UserName, 1, @SecurityStamp)",
                new { user.Nombre, user.Apellido, user.Telefono, user.Clave, user.Imagen, user.Rol, user.Email, user.UserName, SecurityStamp = user.SecurityStamp ?? Guid.NewGuid().ToString() });
            return IdentityResult.Success;
        }

        public async Task<IdentityResult> UpdateAsync(Usuario user, CancellationToken cancellationToken)
        {
            using var cn = Conexion();
            await cn.ExecuteAsync("UPDATE Eurovip.usuario SET email = @Email, clave = @Clave, SecurityStamp = @SecurityStamp WHERE codigoUsuario = @CodigoUsuario",
                new { user.Email, user.Clave, user.SecurityStamp, user.CodigoUsuario });
            return IdentityResult.Success;
        }

        public Task<IdentityResult> DeleteAsync(Usuario user, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<Usuario?> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => _repositorioUsuario.ObtenerPorIdAsync(userId, "codigoUsuario");

        public Task<Usuario?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
            => _repositorioUsuario.GetUserByEmailAsync(normalizedUserName);

        public Task<Usuario?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => _repositorioUsuario.GetUserByEmailAsync(normalizedEmail);

        public Task<string> GetUserIdAsync(Usuario user, CancellationToken cancellationToken) => Task.FromResult(user.CodigoUsuario.ToString());
        public Task<string?> GetUserNameAsync(Usuario user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(Usuario user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(Usuario user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetNormalizedUserNameAsync(Usuario user, string? normalizedName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetEmailAsync(Usuario user, string? email, CancellationToken cancellationToken) { user.Email = email ?? user.Email; return Task.CompletedTask; }
        public Task<string?> GetEmailAsync(Usuario user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.Email);
        public Task<bool> GetEmailConfirmedAsync(Usuario user, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task SetEmailConfirmedAsync(Usuario user, bool confirmed, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<string?> GetNormalizedEmailAsync(Usuario user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.Email);
        public Task SetNormalizedEmailAsync(Usuario user, string? normalizedEmail, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetPasswordHashAsync(Usuario user, string? passwordHash, CancellationToken cancellationToken) { user.Clave = passwordHash ?? user.Clave; return Task.CompletedTask; }
        public Task<string?> GetPasswordHashAsync(Usuario user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.Clave);
        public Task<bool> HasPasswordAsync(Usuario user, CancellationToken cancellationToken) => Task.FromResult(!string.IsNullOrEmpty(user.Clave));

        public Task SetSecurityStampAsync(Usuario user, string stamp, CancellationToken cancellationToken) { user.SecurityStamp = stamp; return Task.CompletedTask; }
        public Task<string?> GetSecurityStampAsync(Usuario user, CancellationToken cancellationToken) => Task.FromResult(user.SecurityStamp);

        public void Dispose() { }
    }
}
