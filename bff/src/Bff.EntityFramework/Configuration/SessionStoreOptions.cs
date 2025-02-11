// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.Bff.EntityFramework;

/// <summary>
/// Options for configuring the session context.
/// </summary>
public class SessionStoreOptions
{
    /// <summary>
    /// Gets or sets the default schema.
    /// </summary>
    /// <value>
    /// The default schema.
    /// </value>
    public string DefaultSchema { get; set; } = null;

    /// <summary>
    /// Gets or sets the persisted grants table configuration.
    /// </summary>
    /// <value>
    /// The persisted grants.
    /// </value>
    public TableConfiguration UserSessions { get; set; } = new TableConfiguration("UserSessions");
}
