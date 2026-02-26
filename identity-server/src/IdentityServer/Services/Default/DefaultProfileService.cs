// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default profile service implementation.
/// This implementation sources all claims from the current subject (e.g. the cookie).
/// </summary>
/// <seealso cref="IProfileService" />
public class DefaultProfileService : IProfileService
{
    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultProfileService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DefaultProfileService(ILogger<DefaultProfileService> logger) => Logger = logger;

    /// <inheritdoc/>
    public virtual Task GetProfileDataAsync(ProfileDataRequestContext context, Ct _)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultProfileService.GetProfileData");

        context.LogProfileRequest(Logger);
        context.AddRequestedClaims(context.Subject.Claims);
        context.LogIssuedClaims(Logger);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public virtual Task IsActiveAsync(IsActiveContext context, Ct _)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultProfileService.IsActive");

        Logger.LogDebug("IsActive called from: {caller}", context.Caller);

        context.IsActive = true;
        return Task.CompletedTask;
    }
}
