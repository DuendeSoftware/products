// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Models;
using System.Linq;
using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.WebUtilities;
using System.Collections.Generic;

namespace Duende.IdentityServer.Hosting;

/// <summary>
/// IdentityServer middleware
/// </summary>
public class IdentityServerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityServerMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next.</param>
    /// <param name="logger">The logger.</param>
    public IdentityServerMiddleware(RequestDelegate next, ILogger<IdentityServerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="options"></param>
    /// <param name="router">The router.</param>
    /// <param name="userSession">The user session.</param>
    /// <param name="events">The event service.</param>
    /// <param name="issuerNameService">The issuer name service</param>
    /// <param name="sessionCoordinationService"></param>
    /// <returns></returns>
    public async Task Invoke(
        HttpContext context, 
        IdentityServerOptions options,
        IEndpointRouter router, 
        IUserSession userSession, 
        IEventService events,
        IIssuerNameService issuerNameService,
        ISessionCoordinationService sessionCoordinationService)
    {
        // this will check the authentication session and from it emit the check session
        // cookie needed from JS-based signout clients.
        await userSession.EnsureSessionIdCookieAsync();

        context.Response.OnStarting(async () =>
        {
            if (context.GetSignOutCalled())
            {
                _logger.LogDebug("SignOutCalled set; processing post-signout session cleanup.");

                // this clears our session id cookie so JS clients can detect the user has signed out
                await userSession.RemoveSessionIdCookieAsync();

                var user = await userSession.GetUserAsync();
                if (user != null)
                {
                    var session = new UserSession
                    {
                        SubjectId = user.GetSubjectId(),
                        SessionId = await userSession.GetSessionIdAsync(),
                        DisplayName = user.GetDisplayName(),
                        ClientIds = (await userSession.GetClientListAsync()).ToList(),
                        Issuer = await issuerNameService.GetCurrentAsync()
                    };
                    await sessionCoordinationService.ProcessLogoutAsync(session);
                }
            }
        });

        try
        {
            var endpoint = router.Find(context);
            if (endpoint != null)
            {
                var endpointType = endpoint.GetType().FullName;
                var requestPath = context.Request.Path.ToString();

                Telemetry.Metrics.IncreaseActiveRequests(endpointType, requestPath);
                try
                {
                    using var activity = Tracing.BasicActivitySource.StartActivity("IdentityServerProtocolRequest");
                    activity?.SetTag(Tracing.Properties.EndpointType, endpointType);

                    // TODO - Consider how to use the new licensing system here
                    IdentityServerLicenseValidator.Instance.ValidateIssuer(await issuerNameService.GetCurrentAsync());

                    _logger.LogInformation("Invoking IdentityServer endpoint: {endpointType} for {url}", endpointType, requestPath);

                    var result = await endpoint.ProcessAsync(context);

                    if (result != null)
                    {
                        _logger.LogTrace("Invoking result: {type}", result.GetType().FullName);
                        await result.ExecuteAsync(context);
                    }

                    return;
                }
                finally
                {
                    Telemetry.Metrics.DecreaseActiveRequests(endpointType, requestPath);
                }
            }
        }
        catch (Exception ex) when (options.Logging.UnhandledExceptionLoggingFilter?.Invoke(context, ex) is not false)
        {
            await events.RaiseAsync(new UnhandledExceptionEvent(ex));
            Telemetry.Metrics.UnHandledException(ex);
            _logger.LogCritical(ex, "Unhandled exception: {exception}", ex.Message);

            throw;
        }

        await _next(context);
    }
}