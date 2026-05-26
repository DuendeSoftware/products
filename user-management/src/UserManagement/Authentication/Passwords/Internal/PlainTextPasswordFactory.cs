// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Passwords.Internal;

internal sealed class PlainTextPasswordFactory(
    IOptions<UserAuthenticationOptions> options,
    IEnumerable<IPasswordValidator> passwordValidators)
{
    private static readonly HashSet<char> Symbols =
    [
        '`', '~', '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '=', '+', '[', '{', ']', '}', '\\', '|',
        ';', ':', '\'', '"', ',', '<', '.', '>', '/', '?'
    ];

    private readonly PasswordOptions _options = options.Value.Passwords;

    public async Task<PasswordCreationResult> CreateAsync(UserSubjectId userId, string passwordString, Ct ct)
    {
        var validationErrors = ValidateComplexity(passwordString);

        if (validationErrors.Count > 0)
        {
            return new PasswordCreationResult.Failed(validationErrors);
        }

        foreach (var validator in passwordValidators)
        {
            if (await validator.ValidateAsync(userId, passwordString, ct) is PasswordValidationResult.Rejected rejected)
            {
                return new PasswordCreationResult.Failed([rejected.Reason]);
            }
        }

        return new PasswordCreationResult.Success(new PlainTextPassword(passwordString, userId));
    }

    private List<string> ValidateComplexity(string passwordString)
    {
        var validationErrors = new List<string>();

        if (passwordString.Length < _options.MinLength)
        {
            validationErrors.Add($"Password must be at least {_options.MinLength} characters.");
        }

        if (passwordString.Length > _options.MaxLength)
        {
            validationErrors.Add($"Password must not exceed {_options.MaxLength} characters.");
        }

        if (passwordString.Count(char.IsUpper) < _options.MinUpper)
        {
            validationErrors.Add($"Password must contain at least {_options.MinUpper} uppercase letter(s).");
        }

        if (passwordString.Count(char.IsLower) < _options.MinLower)
        {
            validationErrors.Add($"Password must contain at least {_options.MinLower} lowercase letter(s).");
        }

        if (passwordString.Count(char.IsAsciiDigit) < _options.MinDigits)
        {
            validationErrors.Add($"Password must contain at least {_options.MinDigits} digit(s).");
        }

        if (passwordString.Count(c => Symbols.Contains(c)) < _options.MinSymbols)
        {
            validationErrors.Add($"Password must contain at least {_options.MinSymbols} symbol(s).");
        }

        return validationErrors;
    }
}
