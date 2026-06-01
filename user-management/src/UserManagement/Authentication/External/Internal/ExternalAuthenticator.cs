// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Internal.Licensing;
using InternalUserAuthenticators = Duende.UserManagement.Authentication.Internal.UserAuthenticators;

namespace Duende.UserManagement.Authentication.External.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class ExternalAuthenticator(
    UserAuthenticatorsRepository authenticatorsRepository,
    UserManagementLicenseValidator licenseValidator) : IExternalAuthenticator
{
    public async Task<ExternalAuthenticationResult> TryAuthenticateAsync(ExternalAuthenticatorAddress address, Ct ct)
    {
        if (!licenseValidator.ValidateExternalIdpLinking())
        {
            UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the External IdP Linking feature.");
        }

        if (await authenticatorsRepository.TryReadAsync(address, ct) is { } authenticatorsRecord)
        {
            return new ExternalAuthenticationResult.Success(authenticatorsRecord.UserAuthenticators.SubjectId);
        }

        var authenticators = new InternalUserAuthenticators(UserSubjectId.New(), [], [address]);
        if (await authenticatorsRepository.CreateAsync(authenticators, ct) is CreateResult.Success)
        {
            licenseValidator.ValidateUserCount();
            return new ExternalAuthenticationResult.Success(authenticators.SubjectId);
        }

        return ExternalAuthenticationResult.Failure.Instance;
    }
}
