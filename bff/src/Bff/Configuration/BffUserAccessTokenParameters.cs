// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using AtmResource = Duende.AccessTokenManagement.Resource;
using Resource = Duende.Bff.AccessTokenManagement.Resource;
using Scheme = Duende.Bff.AccessTokenManagement.Scheme;

namespace Duende.Bff.Configuration;

/// <summary>
/// Additional optional parameters for a user access token request
/// </summary>
public sealed record BffUserAccessTokenParameters
{
    public Scheme? SignInScheme { get; init; }

    public Scheme? ChallengeScheme { get; init; }

    public bool ForceRenewal { get; init; }

    public Resource? Resource { get; init; }


    /// <summary>
    /// Retrieve a UserAccessTokenParameters
    /// </summary>
    /// <returns></returns>
    internal UserTokenRequestParameters ToUserAccessTokenRequestParameters() => new UserTokenRequestParameters()
    {
        SignInScheme = SignInScheme,
        ChallengeScheme = ChallengeScheme,
        ForceTokenRenewal = new ForceTokenRenewal(ForceRenewal),
        Resource = Resource.HasValue ? AtmResource.Parse(Resource.Value.ToString()) : null
    };
}
