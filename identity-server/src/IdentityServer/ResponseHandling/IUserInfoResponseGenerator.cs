// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Generates the response returned from the UserInfo endpoint. The response is a dictionary of
/// claims about the authenticated user, filtered to the scopes and claims that were granted in
/// the access token presented with the request. This interface is invoked after the UserInfo
/// request has been validated and the access token has been introspected.
/// </summary>
/// <remarks>
/// The default implementation retrieves claims from the registered <c>IProfileService</c> and
/// filters them according to the requested scopes. Override this interface or extend the default
/// implementation to add, remove, or transform claims in the UserInfo response, for example to
/// aggregate claims from multiple sources or to apply custom claim transformations.
/// </remarks>
public interface IUserInfoResponseGenerator
{
    /// <summary>
    /// Processes a validated UserInfo request and produces the claims response.
    /// </summary>
    /// <param name="validationResult">
    /// The result of validating the UserInfo request, including the subject, client, and the
    /// scopes and claims granted by the access token.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// A dictionary of claim names to claim values that will be serialized as JSON and returned
    /// from the UserInfo endpoint.
    /// </returns>
    Task<Dictionary<string, object>> ProcessAsync(UserInfoRequestValidationResult validationResult, Ct ct);
}
