// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Administrative interface for managing user authenticator registrations.
/// Provides operations to add, retrieve, and remove authenticators on behalf of users.
/// </summary>
public interface IUserAuthenticatorsAdmin
{
    /// <summary>
    /// Creates a new authenticator record for the specified user with the given OTP addresses and external authenticators.
    /// Returns <c>null</c> if the user already has an authenticator record.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="otpAddresses">The OTP addresses to register.</param>
    /// <param name="externalAuthenticators">The external authenticators to register.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UserAuthenticators?> TryAddAsync(
        UserSubjectId subjectId,
        IEnumerable<OtpAddress> otpAddresses,
        IEnumerable<ExternalAuthenticator> externalAuthenticators,
        Ct ct);

    /// <summary>
    /// Retrieves the authenticator record for the specified user by subject ID.
    /// Returns <c>null</c> if no record exists.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UserAuthenticators?> TryGetAsync(UserSubjectId subjectId, Ct ct);

    /// <summary>
    /// Adds OTP addresses to the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="addresses">The OTP addresses to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryAddOtpAddressesAsync(UserSubjectId subjectId, IEnumerable<OtpAddress> addresses, Ct ct);

    /// <summary>
    /// Removes OTP addresses from the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="addresses">The OTP addresses to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryRemoveOtpAddressesAsync(UserSubjectId subjectId, IEnumerable<OtpAddress> addresses, Ct ct);

    /// <summary>
    /// Adds external authenticators to the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="authenticators">The external authenticators to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> TryAddExternalAuthenticatorsAsync(
        UserSubjectId subjectId, IEnumerable<ExternalAuthenticator> authenticators, Ct ct);

    /// <summary>
    /// Removes external authenticators from the specified user's authenticator record.
    /// Returns <c>false</c> if the user record does not exist.
    /// </summary>
    /// <param name="subjectId">The subject ID of the user.</param>
    /// <param name="authenticators">The external authenticators to remove.</param>
    /// <param name="ct">Cancellation token.</param>
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
