// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;

namespace Duende.Bff;

/// <summary>
/// Additional optional parameters for a user access token request
/// </summary>
public class BffUserAccessTokenParameters(
    string? signInScheme = null,
    string? challengeScheme = null,
    bool forceRenewal = false,
    string? resource = null)
{
    /// <summary>
    /// Retrieve a UserAccessTokenParameters
    /// </summary>
    /// <returns></returns>
    public UserTokenRequestParameters ToUserAccessTokenRequestParameters()
    {
        return new UserTokenRequestParameters()
        {
            SignInScheme = signInScheme,
            ChallengeScheme = challengeScheme,
            ForceRenewal = forceRenewal,
            Resource = resource
        };
    }
}
