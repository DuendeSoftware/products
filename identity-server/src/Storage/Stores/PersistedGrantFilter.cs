// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Represents a filter used when accessing the persisted grants store. 
/// Setting multiple properties is interpreted as a logical 'AND' to further filter the query.
/// At least one value must be supplied.
/// </summary>
public class PersistedGrantFilter
{
    /// <summary>
    /// Subject id of the user.
    /// </summary>
    public string? SubjectId { get; set; }

    /// <summary>
    /// Session id used for the grant.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Client id the grant was issued to.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client ids the grant was issued to.
    /// </summary>
    public IEnumerable<string>? ClientIds { get; set; }

    /// <summary>
    /// The type of grant.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// The types of grants.
    /// </summary>
    public IEnumerable<string>? Types { get; set; }
}
