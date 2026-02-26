// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal interface ISamlSigninStateStore
{
    Task<StateId> StoreSigninRequestStateAsync(SamlAuthenticationState request, Ct ct = default);
    Task<SamlAuthenticationState?> RetrieveSigninRequestStateAsync(StateId stateId, Ct ct = default);
    Task UpdateSigninRequestStateAsync(StateId stateId, SamlAuthenticationState state, Ct ct = default);
}
