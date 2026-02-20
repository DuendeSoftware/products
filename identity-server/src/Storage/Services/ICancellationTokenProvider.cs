// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Services;

/// <summary>
/// Service to provide CancellationToken for async operations.
/// </summary>
public interface ICancellationTokenProvider
{
    /// <summary>
    /// Returns the current CancellationToken, or null if none present.
    /// </summary>
    CT CancellationToken { get; }
}
