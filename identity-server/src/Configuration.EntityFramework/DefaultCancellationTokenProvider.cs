// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Configuration.EntityFramework;

/// <summary>
/// Provides cancellation tokens based on the incoming http request
/// </summary>
#pragma warning disable CA1812 // This class is not instantiated directly, but rather used by the DI container
internal class DefaultCancellationTokenProvider : ICancellationTokenProvider
#pragma warning restore CA1812
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    public DefaultCancellationTokenProvider(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    /// <summary>
    /// Provides access to the cancellation token from the http context
    /// </summary>
    public CancellationToken CancellationToken => _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
}
