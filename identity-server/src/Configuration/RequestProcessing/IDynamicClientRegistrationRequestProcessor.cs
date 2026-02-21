// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;

namespace Duende.IdentityServer.Configuration.RequestProcessing;

/// <summary>
/// Processes valid client registration requests.
/// </summary>
/// <remarks> The request processor is responsible for setting properties of the
/// client that are not specified in the dynamic client registration request,
/// such as the client id and possibly the client secret (when the client secret
/// is not specified as a jwk in the request), and for storing the new client to
/// the <see cref="IClientConfigurationStore"/>.
/// </remarks>
public interface IDynamicClientRegistrationRequestProcessor
{
    /// <summary>
    /// Processes a valid dynamic client registration request, setting
    /// properties of the client that are not specified in the request, and
    /// storing the new client in the <see cref="IClientConfigurationStore"/>.
    /// </summary>
    Task<IDynamicClientRegistrationResponse> ProcessAsync(DynamicClientRegistrationContext validatedRequest, CT ct);
}
