using Microsoft.AspNetCore.Identity;

namespace MoneyBack.Api.Services;

/// <summary>
/// Los mensajes de Identity, en español y dichos como se los diría una
/// persona a otra.
///
/// Por defecto salen en inglés y escritos para quien programa, no para
/// quien usa: a alguien que intentó registrarse le apareció "Username
/// 'correo@gmail.com ' is invalid, can only contain letters or digits" y no
/// tenía forma de saber que lo que sobraba era un espacio al final que le
/// puso el teclado del teléfono.
///
/// La regla del proyecto es que todo lo que ve el usuario va en español, y
/// estos mensajes se escapaban porque los escribe el framework, no nosotros.
/// </summary>
public class ErroresEnEspanol : IdentityErrorDescriber
{
    public override IdentityError DuplicateEmail(string email) => new()
    {
        Code = nameof(DuplicateEmail),
        Description = "Ya hay una cuenta con ese correo. Si es tuya, inicia sesión."
    };

    public override IdentityError DuplicateUserName(string userName) => new()
    {
        Code = nameof(DuplicateUserName),
        Description = "Ya hay una cuenta con ese correo. Si es tuya, inicia sesión."
    };

    public override IdentityError InvalidEmail(string? email) => new()
    {
        Code = nameof(InvalidEmail),
        Description = "Ese correo no parece válido. Revisa que esté completo y sin espacios."
    };

    /// <summary>
    /// Identity llama "username" al correo porque acá son lo mismo. Decirle
    /// "nombre de usuario" a alguien que solo escribió su correo lo manda a
    /// buscar un campo que no existe.
    /// </summary>
    public override IdentityError InvalidUserName(string? userName) => new()
    {
        Code = nameof(InvalidUserName),
        Description = "Ese correo tiene caracteres que no podemos usar. Revisa que no tenga espacios ni tildes."
    };

    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = $"La contraseña necesita al menos {length} caracteres."
    };

    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit),
        Description = "La contraseña necesita al menos un número."
    };

    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower),
        Description = "La contraseña necesita al menos una letra minúscula."
    };

    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper),
        Description = "La contraseña necesita al menos una letra mayúscula."
    };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric),
        Description = "La contraseña necesita al menos un símbolo, como # o !."
    };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new()
    {
        Code = nameof(PasswordRequiresUniqueChars),
        Description = $"La contraseña necesita al menos {uniqueChars} caracteres distintos."
    };

    public override IdentityError PasswordMismatch() => new()
    {
        Code = nameof(PasswordMismatch),
        Description = "La contraseña actual no coincide."
    };

    public override IdentityError InvalidToken() => new()
    {
        Code = nameof(InvalidToken),
        Description = "Ese enlace o código ya no sirve. Pide uno nuevo."
    };

    public override IdentityError UserAlreadyHasPassword() => new()
    {
        Code = nameof(UserAlreadyHasPassword),
        Description = "Esa cuenta ya tiene contraseña."
    };

    public override IdentityError UserLockoutNotEnabled() => new()
    {
        Code = nameof(UserLockoutNotEnabled),
        Description = "Esa cuenta no se puede bloquear."
    };

    public override IdentityError DefaultError() => new()
    {
        Code = nameof(DefaultError),
        Description = "No pudimos completar la operación. Intenta de nuevo."
    };
}
