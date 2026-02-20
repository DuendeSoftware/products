// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Services;

/// <summary>
/// Implementation of ICancellationTokenProvider that returns CancellationToken.None
/// </summary>
public class NoneCancellationTokenProvider : ICancellationTokenProvider
{
    /// <inheritdoc/>
    public CT CancellationToken => CT.None;
}
