// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;

namespace Duende.Bff.Yarp;

public sealed record UserAccessTokenParameters
{
    /// <summary>
    /// The scheme used for signing in the user. This is typically the scheme used for cookie authentication.
    /// </summary>
    public Scheme? SignInScheme { get; init; }

    /// <summary>
    /// The authentication scheme to be used for challenges.
    /// </summary>
    public Scheme? ChallengeScheme { get; init; }

    /// <summary>
    /// Whether to force renewal of the access token.
    /// </summary>
    public bool ForceRenewal { get; init; }

    /// <summary>
    /// The resource for which the access token is requested.
    /// </summary>
    public Resource? Resource { get; init; }
}
