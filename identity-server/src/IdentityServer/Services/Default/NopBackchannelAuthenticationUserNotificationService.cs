// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Nop implementation of IUserLoginService.
/// </summary>
public class NopBackchannelAuthenticationUserNotificationService : IBackchannelAuthenticationUserNotificationService
{
    private readonly IIssuerNameService _issuerNameService;
    private readonly ILogger<NopBackchannelAuthenticationUserNotificationService> _logger;

    /// <summary>
    /// Ctor
    /// </summary>
    public NopBackchannelAuthenticationUserNotificationService(IIssuerNameService issuerNameService, ILogger<NopBackchannelAuthenticationUserNotificationService> logger)
    {
        _issuerNameService = issuerNameService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SendLoginRequestAsync(BackchannelUserLoginRequest request, Ct ct)
    {
        var url = await _issuerNameService.GetCurrentAsync(ct);
        url += "/ciba?id=" + request.InternalId;
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.IBackchannelAuthenticationUserNotificationServiceNotImplementedButForTestingVisitUrl(
                url.SanitizeLogParameter());
        }
    }
}
