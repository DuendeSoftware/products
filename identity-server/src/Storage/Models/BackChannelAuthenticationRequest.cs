// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models a backchannel authentication request.
/// </summary>
public class BackChannelAuthenticationRequest
{
    /// <summary>
    /// Gets or sets the identifier for this request in the store.
    /// </summary>
    public string InternalId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the UTC time when this backchannel authentication request was created.
    /// </summary>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of this backchannel authentication request in seconds.
    /// </summary>
    public int Lifetime { get; set; }

    /// <summary>
    /// Gets or sets the ID of the client that initiated this backchannel authentication request.
    /// </summary>
    public string ClientId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the subject (user) for whom the login request is intended.
    /// </summary>
    public ClaimsPrincipal Subject { get; set; } = default!;

    /// <summary>
    /// Gets or sets the scopes that were requested in this backchannel authentication request.
    /// </summary>
    public IEnumerable<string> RequestedScopes { get; set; } = default!;

    /// <summary>
    /// Gets or sets the resource indicators that were requested in this backchannel authentication request.
    /// </summary>
    public IEnumerable<string>? RequestedResourceIndicators { get; set; }

    /// <summary>
    /// Gets or sets the authentication context reference classes (<c>acr_values</c>) used in this request.
    /// </summary>
    public ICollection<string>? AuthenticationContextReferenceClasses { get; set; }

    /// <summary>
    /// Gets or sets the tenant value extracted from the <c>acr_values</c> used in this request.
    /// </summary>
    public string? Tenant { get; set; }

    /// <summary>
    /// Gets or sets the identity provider (idp) value extracted from the <c>acr_values</c> used in this request.
    /// </summary>
    public string? IdP { get; set; }

    /// <summary>
    /// Gets or sets the binding message used in this request. This is a human-readable identifier
    /// that the client and authorization server display to the user to bind the request to the user's action.
    /// </summary>
    public string? BindingMessage { get; set; }


    /// <summary>
    /// Gets or sets a value indicating whether this backchannel authentication request has been completed
    /// (i.e. the user has approved or denied the request).
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Gets or sets the scopes that the user approved when completing this backchannel authentication request.
    /// <c>null</c> until the request is complete.
    /// </summary>
    public IEnumerable<string>? AuthorizedScopes { get; set; }

    /// <summary>
    /// Gets or sets the session identifier from which the user approved the request.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the description the user assigned to the client being authorized.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets a dictionary of custom properties associated with this
    /// request. These properties by default are copied from the validated
    /// custom request parameters.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}
