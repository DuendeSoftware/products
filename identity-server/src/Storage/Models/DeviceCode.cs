// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Represents data needed for device flow.
/// </summary>
public class DeviceCode
{
    /// <summary>
    /// Gets or sets the UTC time when this device code was created.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of this device code in seconds.
    /// </summary>
    /// <value>
    /// The lifetime.
    /// </value>
    public int Lifetime { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the client that initiated the device flow.
    /// </summary>
    /// <value>
    /// The client identifier.
    /// </value>
    public string ClientId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the description the user assigned to the device being authorized.
    /// </summary>
    /// <value>
    /// The description.
    /// </value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this device code was issued as part of an OpenID Connect flow.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is an OpenID Connect flow; otherwise, <c>false</c>.
    /// </value>
    public bool IsOpenId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user has authorized this device code request.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is authorized; otherwise, <c>false</c>.
    /// </value>
    public bool IsAuthorized { get; set; }

    /// <summary>
    /// Gets or sets the scopes that were requested when this device code was issued.
    /// </summary>
    /// <value>
    /// The authorized scopes.
    /// </value>
    public IEnumerable<string> RequestedScopes { get; set; } = default!;

    /// <summary>
    /// Gets or sets the scopes that the user approved when authorizing this device code.
    /// <c>null</c> until the user has completed authorization.
    /// </summary>
    /// <value>
    /// The authorized scopes.
    /// </value>
    public IEnumerable<string>? AuthorizedScopes { get; set; }

    /// <summary>
    /// Gets or sets the authenticated subject (user) who authorized this device code.
    /// <c>null</c> until the user has completed authorization.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal? Subject { get; set; }

    /// <summary>
    /// Gets or sets the session identifier associated with the user's authentication session when they authorized this device code.
    /// </summary>
    /// <value>
    /// The session identifier.
    /// </value>
    public string? SessionId { get; set; }
}
