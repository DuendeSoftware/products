// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.UserManagement.Scim;

/// <summary>
/// Options for configuring SCIM endpoint authentication and authorization.
/// SCIM endpoints are protected with JWT bearer token authentication per RFC 7644 §2 and §7.4.
/// </summary>
[Experimental(diagnosticId: "duende_experimental",
    Message = "SCIM support is experimental and may change in future releases.")]
public sealed class ScimOAuthOptions
{
    /// <summary>
    /// The authority URL for JWT bearer token validation discovery.
    /// This is used to discover the token signing keys and issuer via the OpenID Connect discovery document.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// The expected audience in bearer tokens.
    /// Defaults to <c>"urn:duende:scim"</c>.
    /// </summary>
    public string Audience { get; set; } = "urn:duende:scim";

    /// <summary>
    /// When set, uses a custom authorization policy instead of the built-in scope-based policies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, the custom policy applies to all SCIM data endpoints (Users, Groups, Bulk).
    /// Metadata/discovery endpoints (ServiceProviderConfig, ResourceTypes, Schemas) always remain
    /// publicly accessible regardless of this setting.
    /// </para>
    /// <para>
    /// The custom policy controls which authentication scheme is used. If the policy does not call
    /// <c>AddAuthenticationSchemes(...)</c>, ASP.NET Core falls back to the application's default
    /// authentication scheme. Ensure the policy references the correct scheme for your auth mechanism
    /// (e.g., API keys, Basic auth).
    /// </para>
    /// <para>
    /// The custom policy must internally differentiate read vs. write operations if that distinction is needed.
    /// </para>
    /// </remarks>
    public string? AuthorizationPolicyName { get; set; }

    /// <summary>
    /// Controls whether the JWT bearer handler requires HTTPS for the metadata/discovery endpoint.
    /// Per RFC 7644 §7.4, bearer tokens MUST be exchanged using TLS.
    /// Only set to <c>false</c> in development environments.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}
