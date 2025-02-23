// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

using System;

namespace Duende.IdentityServer.EntityFramework.Entities;

/// <summary>
/// Models storage for identity providers.
/// </summary>
public class IdentityProvider
{
    /// <summary>
    /// Primary key used for EF
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Scheme name for the provider.
    /// </summary>
    public string Scheme { get; set; }
    /// <summary>
    /// Display name for the provider.
    /// </summary>
    public string DisplayName { get; set; }
    /// <summary>
    /// Flag that indicates if the provider should be used.
    /// </summary>
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// Protocol type of the provider.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Serialized value for the identity provider properties dictionary.
    /// </summary>
    public string Properties { get; set; }

    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? Updated { get; set; }
    public DateTime? LastAccessed { get; set; }
    public bool NonEditable { get; set; }
}
