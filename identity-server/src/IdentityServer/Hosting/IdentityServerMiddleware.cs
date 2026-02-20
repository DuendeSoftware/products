// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Logging;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting;

/// <summary>
/// IdentityServer middleware
/// </summary>
public class IdentityServerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SanitizedLogger<IdentityServerMiddleware> _sanitizedLogger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityServerMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next.</param>
    /// <param name="logger">The logger.</param>
    public IdentityServerMiddleware(RequestDelegate next, ILogger<IdentityServerMiddleware> logger)
    {
        _next = next;
        _sanitizedLogger = new SanitizedLogger<IdentityServerMiddleware>(logger);
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
        await userSession.EnsureSessionIdCookieAsync(context.RequestAborted);

        context.Response.OnStarting(async () =>
        {
            if (context.TryGetExpiredUserSession(out var expiredUserSession))
            {
                _sanitizedLogger.LogDebug("Detected expired session removed; processing post-expiration cleanup.");

                await sessionCoordinationService.ProcessExpirationAsync(expiredUserSession, context.RequestAborted);
            }
        });

        try
        {
            var endpoint = router.Find(context);
            if (endpoint != null)
            {
                var endpointType = endpoint.GetType().FullName;
                var requestPath = context.Items.TryGetValue(MutualTlsEndpointMiddleware.OriginalPath, out var item) ?
                    item?.ToString() :
                    context.Request.Path.ToString();

                Telemetry.Metrics.IncreaseActiveRequests(endpointType, requestPath);
                try
                {
                    var httpActivity = context.Features.Get<IHttpActivityFeature>();
                    if (httpActivity != null)
                    {
                        httpActivity.Activity.DisplayName = $"{context.Request.Method} {requestPath}";
                    }

                    using var activity = Tracing.BasicActivitySource.StartActivity("IdentityServerProtocolRequest");
                    activity?.SetTag(Tracing.Properties.EndpointType, endpointType);

                    var issuer = await issuerNameService.GetCurrentAsync(context.RequestAborted);
                    var licenseUsage = context.RequestServices.GetRequiredService<LicenseUsageTracker>();
                    licenseUsage.IssuerUsed(issuer);
                    IdentityServerLicenseValidator.Instance.ValidateIssuer(issuer);

                    _sanitizedLogger.LogInformation("Invoking IdentityServer endpoint: {endpointType} for {url}", endpointType, requestPath);

                    var result = await endpoint.ProcessAsync(context);

                    if (result != null)
                    {
                        _sanitizedLogger.LogTrace("Invoking result: {type}", result.GetType().FullName);
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
        catch (Exception ex) when (options.Logging.InvokeUnhandledExceptionLoggingFilter(context, ex) is not false)
        {
            await events.RaiseAsync(new UnhandledExceptionEvent(ex), context.RequestAborted);
            Telemetry.Metrics.UnHandledException(ex);
            _sanitizedLogger.LogCritical(ex, "Unhandled exception: {exception}", ex.Message);

            throw;
        }

        await _next(context);
    }
}
