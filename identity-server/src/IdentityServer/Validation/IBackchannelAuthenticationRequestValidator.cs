// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Interface for the backchannel authentication request validator
/// </summary>
public interface IBackchannelAuthenticationRequestValidator
{
    /// <summary>
    /// Validates the request.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="clientValidationResult">The client validation result.</param>
    Task<BackchannelAuthenticationRequestValidationResult> ValidateRequestAsync(NameValueCollection parameters, ClientSecretValidationResult clientValidationResult);
}
