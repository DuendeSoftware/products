// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.Bff.AccessTokenManagement;

/// <summary>
/// Expresses allowed token types
/// </summary>
public enum TokenType
{
    /// <summary>
    /// User token
    /// </summary>
    User,

    /// <summary>
    /// Client token
    /// </summary>
    Client,

    /// <summary>
    /// User or client token
    /// </summary>
    UserOrClient
}
