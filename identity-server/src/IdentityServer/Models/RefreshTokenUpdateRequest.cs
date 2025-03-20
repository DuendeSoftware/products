// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models the data to update a refresh token.
/// </summary>
public class RefreshTokenUpdateRequest
{
    /// <summary>
    /// The handle of the refresh token.
    /// </summary>
    public string Handle { get; set; } = null!;

    /// <summary>
    /// The client.
    /// </summary>
    public Client Client { get; set; } = null!;

    /// <summary>
    /// The refresh token to update.
    /// </summary>
    public RefreshToken RefreshToken { get; set; } = null!;

    /// <summary>
    /// Flag to indicate that the refreth token was modified, and requires an update to the database.
    /// </summary>
    public bool MustUpdate { get; set; }
}
