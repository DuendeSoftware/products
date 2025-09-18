// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.EntityFramework;

/// <summary>
/// Entity class for a user session
/// </summary>
public class UserSessionEntity : UserSession
{
    /// <summary>
    /// Id for record in database
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Discriminator to allow multiple applications to share the user session table.
    /// </summary>
    public string ApplicationName { get; set; }
}
