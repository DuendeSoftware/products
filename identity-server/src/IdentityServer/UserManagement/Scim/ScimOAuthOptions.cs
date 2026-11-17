// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable


namespace Duende.UserManagement.Scim;

/// <summary>
/// Options for configuring SCIM endpoint authentication and authorization.
/// SCIM endpoints are protected with JWT bearer token authentication per RFC 7644 §2 and §7.4.
/// </summary>
public sealed class ScimOAuthOptions
{
    /// <summary>
    /// The authority URL for JWT bearer token validation discovery.
    /// This is used to discover the token signing keys and issuer via the OpenID Connect discovery document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inside IdentityServer, when left unset, SCIM bearer tokens are validated in-process against the
    /// current request's issuer and signing keys, with no discovery call over HTTP. This means a token
    /// issued for one space is rejected by every other space.
    /// </para>
    /// <para>
    /// When set, SCIM bearer tokens are validated against this one fixed issuer for every request,
    /// regardless of which space served it. With Spaces registered, this is only allowed together with
    /// custom read and write authorization policies, because otherwise every space would accept tokens
    /// from that one authority.
    /// </para>
    /// </remarks>
    public string? Authority { get; set; }

    /// <summary>
    /// The expected audience in bearer tokens.
    /// Defaults to <c>"urn:duende:scim"</c>.
    /// </summary>
    public string Audience { get; set; } = "urn:duende:scim";

    /// <summary>
    /// When set, uses a custom authorization policy instead of the built-in scope-based policies, for
    /// both reads and writes. <see cref="ReadAuthorizationPolicyName"/> and
    /// <see cref="WriteAuthorizationPolicyName"/> take precedence over this on their respective side.
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
    /// This is the fallback policy for both reads and writes. Set <see cref="ReadAuthorizationPolicyName"/>
    /// and/or <see cref="WriteAuthorizationPolicyName"/> instead when reads and writes need different
    /// policies; this property no longer needs to internally differentiate read vs. write operations when
    /// those are set.
    /// </para>
    /// </remarks>
    public string? AuthorizationPolicyName { get; set; }

    /// <summary>
    /// When set, uses a custom authorization policy for SCIM reads (GET list, GET by id, and
    /// <c>POST .../.search</c>) instead of <see cref="AuthorizationPolicyName"/> or the built-in
    /// <c>DuendeScimRead</c> policy.
    /// </summary>
    /// <remarks>
    /// Resolution order for reads: <see cref="ReadAuthorizationPolicyName"/>, then
    /// <see cref="AuthorizationPolicyName"/>, then the built-in <c>DuendeScimRead</c> policy. A blank
    /// value is treated as unset. See <see cref="AuthorizationPolicyName"/> for how a custom policy
    /// controls the authentication scheme; metadata/discovery endpoints always stay anonymous.
    /// </remarks>
    public string? ReadAuthorizationPolicyName { get; set; }

    /// <summary>
    /// When set, uses a custom authorization policy for SCIM writes (POST, PUT, PATCH, DELETE, and
    /// <c>POST /Bulk</c>) instead of <see cref="AuthorizationPolicyName"/> or the built-in
    /// <c>DuendeScimWrite</c> policy.
    /// </summary>
    /// <remarks>
    /// Resolution order for writes: <see cref="WriteAuthorizationPolicyName"/>, then
    /// <see cref="AuthorizationPolicyName"/>, then the built-in <c>DuendeScimWrite</c> policy. A blank
    /// value is treated as unset. See <see cref="AuthorizationPolicyName"/> for how a custom policy
    /// controls the authentication scheme; metadata/discovery endpoints always stay anonymous.
    /// </remarks>
    public string? WriteAuthorizationPolicyName { get; set; }

    /// <summary>
    /// Controls whether the JWT bearer handler requires HTTPS for the metadata/discovery endpoint.
    /// Per RFC 7644 §7.4, bearer tokens MUST be exchanged using TLS.
    /// Only set to <c>false</c> in development environments.
    /// Applies only when <see cref="Authority"/> is set.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}
