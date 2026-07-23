// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.UserManagement;

/// <summary>
/// Provides pre-built IdentityServer resource definitions for the SCIM API.
/// Use these when configuring IdentityServer's resource store to enable
/// OAuth 2.0 protection of SCIM endpoints.
/// </summary>
[Experimental(diagnosticId: "duende_experimental",
    Message = "SCIM support is experimental and may change in future releases.")]
public static class ScimResources
{
    /// <summary>
    /// The default audience value for the SCIM API resource.
    /// </summary>
    public const string Audience = "urn:duende:scim";

    /// <summary>
    /// The scope name granting full read and write access to SCIM resources.
    /// </summary>
    public const string FullAccessScopeName = "scim";

    /// <summary>
    /// The scope name granting read-only access to SCIM resources.
    /// </summary>
    public const string ReadOnlyScopeName = "scim.read";

    /// <summary>
    /// Gets an <see cref="ApiResource"/> representing the SCIM API.
    /// The resource uses <see cref="Audience"/> as its name and includes
    /// both <see cref="FullAccessScopeName"/> and <see cref="ReadOnlyScopeName"/>.
    /// </summary>
    public static ApiResource ApiResource => new(Audience, "Duende SCIM API")
    {
        Scopes = { FullAccessScopeName, ReadOnlyScopeName }
    };

    /// <summary>
    /// Gets an <see cref="ApiScope"/> granting full read and write access to all SCIM resources.
    /// </summary>
    public static ApiScope FullAccessScope => new(FullAccessScopeName, "SCIM - Full Access");

    /// <summary>
    /// Gets an <see cref="ApiScope"/> granting read-only access to SCIM resources.
    /// </summary>
    public static ApiScope ReadOnlyScope => new(ReadOnlyScopeName, "SCIM - Read Only");
}
