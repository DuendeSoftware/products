// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.Bff.AccessTokenManagement;

/// <summary>
/// Represents an error that occurred during the retrieval of an access token.
/// </summary>
public record AccessTokenRetrievalError : AccessTokenResult
{
    public required string Error { get; init; }

    public string? ErrorDescription { get; init; }
}
