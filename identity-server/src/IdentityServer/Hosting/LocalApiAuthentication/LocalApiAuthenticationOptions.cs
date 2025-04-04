// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.AspNetCore.Authentication;

namespace Duende.IdentityServer.Hosting.LocalApiAuthentication;

/// <summary>
/// Options for local API authentication
/// </summary>
/// <seealso cref="Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions" />
public class LocalApiAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Indicates if bearer and/or DPoP tokens are accepted.
    /// </summary>
    public LocalApiTokenMode TokenMode { get; set; } = LocalApiTokenMode.BearerOnly;

    /// <summary>
    /// Allows setting a specific required scope (optional)
    /// </summary>
    public string? ExpectedScope { get; set; }

    /// <summary>
    /// Specifies whether the token should be saved in the authentication properties
    /// </summary>
    public bool SaveToken { get; set; } = true;

    /// <summary>
    /// Allows implementing events
    /// </summary>
    public new LocalApiAuthenticationEvents Events
    {
        get => (LocalApiAuthenticationEvents)base.Events!;
        set => base.Events = value;
    }
}
