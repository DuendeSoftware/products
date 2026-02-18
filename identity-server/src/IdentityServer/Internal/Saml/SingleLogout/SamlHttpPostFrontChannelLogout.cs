// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

internal class SamlHttpPostFrontChannelLogout(Uri frontChannelLogoutUri, string logoutRequest, string? relayState) : ISamlFrontChannelLogout
{
    public SamlBinding SamlBinding => SamlBinding.HttpPost;

    public Uri Destination => frontChannelLogoutUri;

    public string EncodedContent => logoutRequest;

    public string? RelayState => relayState;
}
