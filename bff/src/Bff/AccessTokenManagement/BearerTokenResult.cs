// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.Bff.AccessTokenManagement;


/// <summary>
/// Represents a bearer token result obtained during access token retrieval.
/// </summary>
public sealed record BearerTokenResult : AccessTokenResult
{
    /// <summary>
    /// The access token.
    /// </summary>
    public required AccessToken AccessToken { get; init; }

}

