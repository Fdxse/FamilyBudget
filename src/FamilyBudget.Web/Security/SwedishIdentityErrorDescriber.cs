using Microsoft.AspNetCore.Identity;

namespace FamilyBudget.Web.Security;

/// <summary>
/// Swedish texts for the Identity errors a user is most likely to see.
/// Anything not overridden falls back to the English default.
/// </summary>
public class SwedishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"Användarnamnet '{userName}' är redan upptaget." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = $"Användarnamnet '{userName}' innehåller otillåtna tecken. Använd bokstäver, siffror, - . _ @" };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Lösenordet måste vara minst {length} tecken." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Fel nuvarande lösenord." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "Lösenordet måste innehålla minst en siffra." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Lösenordet måste innehålla minst en liten bokstav." };
}
