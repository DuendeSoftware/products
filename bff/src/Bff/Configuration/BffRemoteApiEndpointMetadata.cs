// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Endpoints;

namespace Duende.Bff.Configuration;

/// <summary>
/// Endpoint metadata for a remote BFF API endpoint
/// </summary>
public sealed class BffRemoteApiEndpointMetadata : IBffApiMetadata
{
    /// <summary>
    /// Required token type (if any)
    /// </summary>
    public RequiredTokenType? TokenType { get; set; }

    /// <summary>
    /// Maps to UserAccessTokenParameters and included if set
    /// </summary>
    public BffUserAccessTokenParameters? BffUserAccessTokenParameters { get; set; }

    /// <summary>
    /// The type used to retrieve access tokens.
    /// </summary>
    public Type AccessTokenRetriever
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.IsAssignableTo(typeof(IAccessTokenRetriever)))
            {
                field = value;
            }
            else
            {
                throw new InvalidOperationException(
                    "Attempt to assign a AccessTokenRetriever type that cannot be assigned to IAccessTokenTokenRetriever");
            }
        }
    } = typeof(IAccessTokenRetriever);
}
