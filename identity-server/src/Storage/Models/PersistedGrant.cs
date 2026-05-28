// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// A model for a persisted grant
/// </summary>
public class PersistedGrant
{
    /// <summary>
    /// Gets or sets the unique key that identifies this grant in the store.
    /// </summary>
    /// <value>
    /// The key.
    /// </value>
    public string Key { get; set; } = default!;

    /// <summary>
    /// Gets or sets the grant type (e.g. <c>authorization_code</c>, <c>refresh_token</c>, <c>reference_token</c>).
    /// </summary>
    /// <value>
    /// The type.
    /// </value>
    public string Type { get; set; } = default!;

    /// <summary>
    /// Gets or sets the subject identifier of the user associated with this grant.
    /// </summary>
    /// <value>
    /// The subject identifier.
    /// </value>
    public string SubjectId { get; set; } = default!;

    /// <summary>
    /// Gets or sets the session identifier associated with this grant, if any.
    /// </summary>
    /// <value>
    /// The session identifier.
    /// </value>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the client identifier that requested this grant.
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
    /// Gets or sets the UTC time when this grant was created.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    public DateTime CreationTime { get; set; }

    /// <summary>
    /// Gets or sets the UTC time when this grant expires. <c>null</c> means the grant does not expire.
    /// </summary>
    /// <value>
    /// The expiration.
    /// </value>
    public DateTime? Expiration { get; set; }

    /// <summary>
    /// Gets or sets the UTC time when this grant was consumed (e.g. a one-time-use authorization code was redeemed).
    /// <c>null</c> means the grant has not been consumed.
    /// </summary>
    /// <value>
    /// The consumed time.
    /// </value>
    public DateTime? ConsumedTime { get; set; }

    /// <summary>
    /// Gets or sets the serialized payload of the grant (e.g. the JSON-serialized token or code data).
    /// </summary>
    /// <value>
    /// The data.
    /// </value>
    public string Data { get; set; } = default!;
}
