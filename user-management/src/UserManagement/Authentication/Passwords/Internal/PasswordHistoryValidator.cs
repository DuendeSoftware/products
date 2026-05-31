// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal.Storage;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Passwords.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class PasswordHistoryValidator(
    UserAuthenticatorsRepository repository,
    PasswordHashAlgorithms passwordHashAlgorithms,
    IOptions<UserAuthenticationOptions> options)
    : IPasswordValidator
{
    public async Task<PasswordValidationResult> ValidateAsync(UserSubjectId userId, string password, Ct ct)
    {
        var historyCount = options.Value.Passwords.HistoryCount;
        if (historyCount <= 0)
        {
            return new PasswordValidationResult.Accepted();
        }

        var record = await repository.TryReadAsync(userId, ct);
        if (record is null)
        {
            return new PasswordValidationResult.Accepted();
        }

        return record.Value.UserAuthenticators.MatchesPasswordHistory(password, historyCount, passwordHashAlgorithms.All)
            ? new PasswordValidationResult.Rejected("Password has been used recently and cannot be reused.")
            : new PasswordValidationResult.Accepted();
    }
}
