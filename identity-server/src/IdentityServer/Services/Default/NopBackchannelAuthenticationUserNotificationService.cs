// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Logging;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Nop implementation of IUserLoginService.
/// </summary>
public class NopBackchannelAuthenticationUserNotificationService : IBackchannelAuthenticationUserNotificationService
{
    private readonly IIssuerNameService _issuerNameService;
    private readonly SanitizedLogger<NopBackchannelAuthenticationUserNotificationService> _sanitizedLogger;

    /// <summary>
    /// Ctor
    /// </summary>
    public NopBackchannelAuthenticationUserNotificationService(IIssuerNameService issuerNameService, ILogger<NopBackchannelAuthenticationUserNotificationService> logger)
    {
        _issuerNameService = issuerNameService;
        _sanitizedLogger = new SanitizedLogger<NopBackchannelAuthenticationUserNotificationService>(logger);
    }

    /// <inheritdoc/>
    public async Task SendLoginRequestAsync(BackchannelUserLoginRequest request)
    {
        var url = await _issuerNameService.GetCurrentAsync(default);
        url += "/ciba?id=" + request.InternalId;
        _sanitizedLogger.LogWarning("IBackchannelAuthenticationUserNotificationService not implemented. But for testing, visit {url} to simulate what a user might need to do to complete the request.", url);
    }
}
