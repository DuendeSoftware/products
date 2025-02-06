// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;

namespace Duende.Bff;

/// <summary>
/// Endpoint metadata for a remote BFF API endpoint
/// </summary>
public class BffRemoteApiEndpointMetadata : IBffApiEndpoint
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

    private Type _accessTokenRetriever = typeof(DefaultAccessTokenRetriever);

    /// <summary>
    /// The type used to retrieve access tokens.
    /// </summary>
    public Type AccessTokenRetriever
    {
        get
        {
            return _accessTokenRetriever;
        }
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