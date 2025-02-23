// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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
    public async Task SendLoginRequestAsync(BackchannelUserLoginRequest request)
    {
        var url = await _issuerNameService.GetCurrentAsync();
        url += "/ciba?id=" + request.InternalId;
        _logger.LogWarning("IBackchannelAuthenticationUserNotificationService not implemented. But for testing, visit {url} to simulate what a user might need to do to complete the request.", url);
    }
}