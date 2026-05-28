// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace IdentityServerHost;

/// <summary>
/// A custom identity provider type for WS-Federation providers, demonstrating
/// how to extend the dynamic provider system with a custom protocol type.
/// </summary>
public record WsFedProvider : IdentityProvider
{
    public WsFedProvider() : base("wsfed")
    {
    }

    public WsFedProvider(IdentityProvider other) : base("wsfed", other)
    {
    }

    public string? MetadataAddress
    {
        get => this["MetadataAddress"];
        set => this["MetadataAddress"] = value;
    }

    public string? Wtrealm
    {
        get => this["Wtrealm"];
        set => this["Wtrealm"] = value;
    }
}
