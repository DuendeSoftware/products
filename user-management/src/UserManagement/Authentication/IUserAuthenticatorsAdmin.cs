// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;

namespace Duende.UserManagement.Authentication;

public interface IUserAuthenticatorsAdmin
{
    Task<UserAuthenticators?> TryAddAsync(
        UserSubjectId subjectId,
        IEnumerable<OtpAddress> otpAddresses,
        IEnumerable<ExternalAuthenticator> externalAuthenticators,
        Ct ct);

    Task<UserAuthenticators?> TryGetAsync(UserSubjectId subjectId, Ct ct);

    Task<UserAuthenticators?> TryGetAsync(UserName userName, Ct ct);

    Task<bool> TryAddOtpAddressesAsync(UserSubjectId subjectId, IEnumerable<OtpAddress> addresses, Ct ct);

    Task<bool> TryRemoveOtpAddressesAsync(UserSubjectId subjectId, IEnumerable<OtpAddress> addresses, Ct ct);

    Task<bool> TryAddExternalAuthenticatorsAsync(
        UserSubjectId subjectId, IEnumerable<ExternalAuthenticator> authenticators, Ct ct);

    Task<bool> TryRemoveExternalAuthenticatorsAsync(
        UserSubjectId subjectId, IEnumerable<ExternalAuthenticator> authenticators, Ct ct);

    /// <summary>
    /// Queries authenticator records. Filtering and sorting are not supported and cause <see cref="NotSupportedException" />.
    /// Only <see cref="QueryRequest.Range" /> is used.
    /// </summary>
    Task<QueryResult<UserAuthenticators>> QueryAsync(
        QueryRequest request,
        Ct ct);
}
