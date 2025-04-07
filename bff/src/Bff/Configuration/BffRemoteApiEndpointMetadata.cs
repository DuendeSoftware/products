// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Endpoints;

namespace Duende.Bff.Configuration;

/// <summary>
/// Endpoint metadata for a remote BFF API endpoint
/// </summary>
public class BffRemoteApiEndpointMetadata : IBffApiMetadata
{
    /// <summary>
    /// Required token type (if any)
    /// </summary>
    public TokenType? RequiredTokenType;

    /// <summary>
    /// Optionally send a user token if present
    /// </summary>
    public bool OptionalUserToken { get; set; }

    /// <summary>
    /// Maps to UserAccessTokenParameters and included if set
    /// </summary>
    public BffUserAccessTokenParameters? BffUserAccessTokenParameters { get; set; }

    private Type _accessTokenRetriever = typeof(IAccessTokenRetriever);

    /// <summary>
    /// The type used to retrieve access tokens.
    /// </summary>
    public Type AccessTokenRetriever
    {
        get => _accessTokenRetriever;
        set
        {
            if (value.IsAssignableTo(typeof(IAccessTokenRetriever)))
            {
                _accessTokenRetriever = value;
            }
            else
            {
                throw new Exception("Attempt to assign a AccessTokenRetriever type that cannot be assigned to IAccessTokenTokenRetriever");
            }
        }
    }
}
