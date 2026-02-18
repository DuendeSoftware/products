// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

internal class SamlHttpRedirectFrontChannelLogout(Uri frontChannelLogoutUri, string encodedContent) : ISamlFrontChannelLogout
{
    public SamlBinding SamlBinding => SamlBinding.HttpRedirect;

    public Uri Destination => frontChannelLogoutUri;

    public string EncodedContent => encodedContent;

    public string? RelayState { get; }
}
