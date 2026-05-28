// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Generates the response returned from the device authorization endpoint (RFC 8628). The
/// response contains the device code, user code, verification URI, and polling interval that
/// the device uses to poll the token endpoint while the user completes authorization on a
/// separate device. This interface is invoked after the device authorization request has been
/// validated.
/// </summary>
/// <remarks>
/// The default implementation creates the device code and user code, stores them, and
/// constructs the verification URIs. Override this interface or extend the default
/// implementation to customize the device authorization response, for example to change the
/// format of the user code or to add custom properties to the response.
/// </remarks>
public interface IDeviceAuthorizationResponseGenerator
{
    /// <summary>
    /// Processes a validated device authorization request and produces the device authorization
    /// response.
    /// </summary>
    /// <param name="validationResult">
    /// The result of validating the device authorization request, including the client and
    /// requested scopes.
    /// </param>
    /// <param name="baseUrl">
    /// The base URL of the IdentityServer instance, used to construct the verification URI
    /// that is included in the response.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A <see cref="DeviceAuthorizationResponse"/> containing the device code, user code,
    /// verification URI, verification URI with the user code embedded, expiration, and polling
    /// interval.
    /// </returns>
    Task<DeviceAuthorizationResponse> ProcessAsync(DeviceAuthorizationRequestValidationResult validationResult, string baseUrl, Ct ct);
}
