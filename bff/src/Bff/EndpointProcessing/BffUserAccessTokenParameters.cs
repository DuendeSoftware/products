// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;

namespace Duende.Bff;

/// <summary>
/// Additional optional parameters for a user access token request
/// </summary>
public class BffUserAccessTokenParameters
{
    /// <summary>
    /// All properties are private with the sole intention being the transfer to a UserAccessTokenParameters via ToUserAccessTokenParameters
    /// </summary>
    /// <param name="signInScheme"></param>
    /// <param name="challengeScheme"></param>
    /// <param name="forceRenewal"></param>
    /// <param name="resource"></param>
    public BffUserAccessTokenParameters(
        string? signInScheme = null,
        string? challengeScheme = null,
        bool forceRenewal = false,
        string? resource = null)
    {
        SignInScheme = signInScheme;
        ChallengeScheme = challengeScheme;
        ForceRenewal = forceRenewal;
        Resource = resource;
    }

    /// <summary>
    /// Overrides the default sign-in scheme. This information may be used for state management.
    /// </summary>
    private string? SignInScheme { get; }

    /// <summary>
    /// Overrides the default challenge scheme. This information may be used for deriving token service configuration.
    /// </summary>
    private string? ChallengeScheme { get; }

    /// <summary>
    /// Force renewal of token.
    /// </summary>
    private bool ForceRenewal { get; }

    /// <summary>
    /// Specifies the resource parameter.
    /// </summary>
    private string? Resource { get; }

    /// <summary>
    /// Retrieve a UserAccessTokenParameters
    /// </summary>
    /// <returns></returns>
    public UserTokenRequestParameters ToUserAccessTokenRequestParameters()
    {
        return new UserTokenRequestParameters()
        {
            SignInScheme = this.SignInScheme,
            ChallengeScheme = this.ChallengeScheme,
            ForceRenewal = this.ForceRenewal,
            Resource = this.Resource
        };
    }
}
