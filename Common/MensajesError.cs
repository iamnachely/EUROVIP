using Microsoft.AspNetCore.Identity;

namespace Common
{
    /// <summary>Mensajes de Identity en español (copiado del patrón de BackTiTan).</summary>
    public class MensajesError : IdentityErrorDescriber
    {
        public override IdentityError DefaultError() => new() { Code = nameof(DefaultError), Description = "Ha ocurrido un error." };
        public override IdentityError ConcurrencyFailure() => new() { Code = nameof(ConcurrencyFailure), Description = "Ha ocurrido un error, el objeto ya ha sido modificado." };
        public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "Contraseña incorrecta." };
        public override IdentityError InvalidToken() => new() { Code = nameof(InvalidToken), Description = "Ha ingresado un código inválido." };
        public override IdentityError InvalidUserName(string? userName) => new() { Code = nameof(InvalidUserName), Description = $"El nombre de usuario '{userName}' es inválido." };
        public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = $"La dirección de email '{email}' es incorrecta." };
        public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = $"El usuario '{userName}' ya existe." };
        public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = $"El email '{email}' ya se encuentra registrado." };
        public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener mínimo {length} caracteres." };
        public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "La contraseña debe contener al menos un símbolo." };
        public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe incluir al menos un dígito ('0'-'9')." };
        public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe incluir al menos una minúscula." };
        public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "La contraseña debe incluir al menos una MAYÚSCULA." };
    }
}
