// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default implementation of logout notification service.
/// </summary>
public class LogoutNotificationService : ILogoutNotificationService
{
    private readonly IClientStore _clientStore;
    private readonly IIssuerNameService _issuerNameService;
    private readonly ILogger<LogoutNotificationService> _logger;


    /// <summary>
    /// Ctor.
    /// </summary>
    public LogoutNotificationService(
        IClientStore clientStore,
        IIssuerNameService issuerNameService,
        ILogger<LogoutNotificationService> logger)
    {
        _clientStore = clientStore;
        _issuerNameService = issuerNameService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetFrontChannelLogoutNotificationsUrlsAsync(LogoutNotificationContext context, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("LogoutNotificationService.GetFrontChannelLogoutNotificationsUrls");

        var frontChannelUrls = new List<string>();
        foreach (var clientId in context.ClientIds)
        {
            var client = await _clientStore.FindEnabledClientByIdAsync(clientId, ct);
            if (client != null)
            {
                if (client.FrontChannelLogoutUri.IsPresent())
                {
                    var url = client.FrontChannelLogoutUri;

                    // add session id if required
                    if (client.ProtocolType == IdentityServerConstants.ProtocolTypes.OpenIdConnect)
                    {
                        if (client.FrontChannelLogoutSessionRequired)
                        {
                            url = url.AddQueryString(OidcConstants.EndSessionRequest.Sid, context.SessionId);
                            url = url.AddQueryString(OidcConstants.EndSessionRequest.Issuer, await _issuerNameService.GetCurrentAsync(ct));
                        }
                    }
                    else if (client.ProtocolType == IdentityServerConstants.ProtocolTypes.WsFederation)
                    {
                        url = url.AddQueryString(Constants.WsFedSignOut.LogoutUriParameterName, Constants.WsFedSignOut.LogoutUriParameterValue);
                    }

                    frontChannelUrls.Add(url);
                }
            }
        }

        if (frontChannelUrls.Count > 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var msg = frontChannelUrls.Aggregate((x, y) => x + ", " + y);
                _logger.ClientFrontChannelLogoutURLsValue(msg.SanitizeLogParameter());
            }
        }
        else
        {
            _logger.NoClientFrontChannelLogoutURLs();
        }

        return frontChannelUrls;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<BackChannelLogoutRequest>> GetBackChannelLogoutNotificationsAsync(LogoutNotificationContext context, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("LogoutNotificationService.GetBackChannelLogoutNotifications");

        var backChannelLogouts = new List<BackChannelLogoutRequest>();
        foreach (var clientId in context.ClientIds)
        {
            var client = await _clientStore.FindEnabledClientByIdAsync(clientId, ct);
            if (client != null)
            {
                if (client.BackChannelLogoutUri.IsPresent())
                {
                    var back = new BackChannelLogoutRequest
                    {
                        ClientId = clientId,
                        LogoutUri = client.BackChannelLogoutUri,
                        SubjectId = context.SubjectId,
                        SessionId = context.SessionId,
                        SessionIdRequired = client.BackChannelLogoutSessionRequired,
                        Issuer = context.Issuer,
                        LogoutReason = context.LogoutReason,
                    };

                    backChannelLogouts.Add(back);
                }
            }
        }

        if (backChannelLogouts.Count > 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var msg = backChannelLogouts.Select(x => x.LogoutUri).Aggregate((x, y) => x + ", " + y);
                _logger.ClientBackChannelLogoutURLsValue(msg.SanitizeLogParameter());
            }
        }
        else
        {
            _logger.NoClientBackChannelLogoutURLs();
        }

        return backChannelLogouts;
    }
}
