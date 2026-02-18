// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Saml;

public interface ISamlSigninInteractionResponseGenerator
{
    Task<SamlInteractionResponse> ProcessInteractionAsync(SamlServiceProvider sp, AuthNRequest request, CancellationToken ct);
}
