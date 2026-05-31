// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Operations;
using Duende.Storage.Querying;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Internal;

#pragma warning disable CS1573 // Parameter 'parameter' has no matching param tag in the XML comment for 'parameter' (but other parameters do)
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserAuthenticatorsAdmin(
    UserAuthenticatorsRepository repo,
    ILogger<UserAuthenticatorsAdmin> logger,
    UserManagementLicenseValidator licenseValidator) : IUserAuthenticatorsAdmin
{
    public async Task<Authentication.UserAuthenticators?> TryAddAsync(
        UserSubjectId subjectId,
        IEnumerable<OtpAddress> otpAddresses,
        IEnumerable<ExternalAuthenticator> externalAuthenticators,
        Ct ct)
    {
        var materializedExternalAuthenticators =
            externalAuthenticators as ExternalAuthenticator[] ?? externalAuthenticators.ToArray();
        if (materializedExternalAuthenticators.Length != 0)
        {
            licenseValidator.ValidateExternalIdpLinking();
        }

        var materializedOtpAddresses = otpAddresses as OtpAddress[] ?? otpAddresses.ToArray();
        if (materializedOtpAddresses.Length != 0)
        {
            licenseValidator.ValidateOtp();
        }

        using var scope = logger.BeginSubjectScope(subjectId);
        var user = new UserAuthenticators(subjectId, materializedOtpAddresses, materializedExternalAuthenticators);
        if (await repo.CreateAsync(user, ct) is CreateResult.Success)
        {
            licenseValidator.ValidateUserCount();
            return new Authentication.UserAuthenticators(user);
        }

        return null;
    }

    public async Task<Authentication.UserAuthenticators?> TryGetAsync(UserSubjectId subjectId, Ct ct)
    {
        using var scope = logger.BeginSubjectScope(subjectId);
        return await repo.TryReadAsync(subjectId, ct) is ({ } user, _)
            ? new Authentication.UserAuthenticators(user)
            : null;
    }

    public async Task<bool> TryAddOtpAddressesAsync(
        UserSubjectId subjectId, IEnumerable<OtpAddress> addresses, Ct ct)
    {
        licenseValidator.ValidateOtp();
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not ({ } user, var version))
        {
            return false;
        }

        user.Add(addresses);
        var result = await repo.UpdateAsync(user, version, ct) is UpdateResult.Success;
        if (result)
        {
            logger.OtpAddressAdded(LogLevel.Information, subjectId);
        }

        return result;
    }

    public async Task<bool> TryRemoveOtpAddressesAsync(
        UserSubjectId subjectId, IEnumerable<OtpAddress> addresses, Ct ct)
    {
        licenseValidator.ValidateOtp();
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not ({ } user, var version))
        {
            return false;
        }

        user.Remove(addresses);
        var result = await repo.UpdateAsync(user, version, ct) is UpdateResult.Success;
        if (result)
        {
            logger.OtpAddressRemoved(LogLevel.Information, subjectId);
        }

        return result;
    }

    public async Task<bool> TryAddExternalAuthenticatorsAsync(
        UserSubjectId subjectId, IEnumerable<ExternalAuthenticator> authenticators, Ct ct)
    {
        licenseValidator.ValidateExternalIdpLinking();
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not ({ } user, var version))
        {
            return false;
        }

        user.Add(authenticators);
        var addResult = await repo.UpdateAsync(user, version, ct) is UpdateResult.Success;
        if (addResult)
        {
            logger.ExternalAuthenticatorAdded(LogLevel.Information, subjectId);
        }

        return addResult;
    }

    public async Task<bool> TryRemoveExternalAuthenticatorsAsync(
        UserSubjectId subjectId, IEnumerable<ExternalAuthenticator> authenticators, Ct ct)
    {
        licenseValidator.ValidateExternalIdpLinking();
        using var scope = logger.BeginSubjectScope(subjectId);
        if (await repo.TryReadAsync(subjectId, ct) is not ({ } user, var version))
        {
            return false;
        }

        user.Remove(authenticators);
        var removeResult = await repo.UpdateAsync(user, version, ct) is UpdateResult.Success;
        if (removeResult)
        {
            logger.ExternalAuthenticatorRemoved(LogLevel.Information, subjectId);
        }

        return removeResult;
    }

    public async Task<QueryResult<Authentication.UserAuthenticators>> QueryAsync(QueryRequest request, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Filter is not null || request.Sort is not null)
        {
            throw new NotSupportedException("User authenticator queries do not support filtering or sorting.");
        }

        var result = await repo.QueryAsync(request.Range, ct);
        return result.ConvertTo(user => new Authentication.UserAuthenticators(user));
    }
}
