// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal.Storage;

namespace Duende.UserManagement.Authentication.Internal;

internal enum UserAuthenticatorsImportResult
{
    Success,
    AlreadyExists,
    Failed
}

internal sealed class UserAuthenticatorsImportService(UserAuthenticatorsRepository repository)
{
    internal async Task<UserAuthenticatorsImportResult> ImportAsync(UserAuthenticators authenticators, Ct ct) =>
        await repository.CreateAsync(authenticators, ct).ConfigureAwait(false) switch
        {
            CreateResult.Success => UserAuthenticatorsImportResult.Success,
            CreateResult.AlreadyExists or CreateResult.KeyConflict => UserAuthenticatorsImportResult.AlreadyExists,
            _ => UserAuthenticatorsImportResult.Failed
        };
}
