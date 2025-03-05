// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options for DPoP
/// </summary>
public class DPoPOptions
{
    /// <summary>
    /// Duration that DPoP proof tokens are considered valid. Defaults to 1 minute.
    /// </summary>
    public TimeSpan ProofTokenValidityDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Clock skew used in validating DPoP proof token expiration using a server-generated nonce value. Defaults to ten seconds.
    /// </summary>
    public TimeSpan ServerClockSkew { get; set; } = TimeSpan.FromSeconds(10);
}
