// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml;

public interface ISamlFrontChannelLogout
{
    SamlBinding SamlBinding { get; }

    Uri Destination { get; }

    string EncodedContent { get; }

    string? RelayState { get; }
}
