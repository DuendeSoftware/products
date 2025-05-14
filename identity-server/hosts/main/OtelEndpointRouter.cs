// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable CA1848
#pragma warning disable CA2254
#pragma warning disable CA1812
#pragma warning disable CA1852

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics;
using System.Reflection;

namespace IdentityServerHost;

internal class OTelEndpointRouter(
    IEnumerable<Duende.IdentityServer.Hosting.Endpoint> endpoints,
    IdentityServerOptions options,
    ILogger<OTelEndpointRouter> logger)
    : IEndpointRouter
{
    public const string TraceName = "EndpointDetails";

    public static ActivitySource EndpointActivitySource { get; } = new(
        TraceName, typeof(IEndpointRouter).Assembly.GetName().Version!.ToString());

    private static readonly Type protocolRequestCounterType = typeof(IEndpointRouter).Assembly
        .GetType("Duende.IdentityServer.Licensing.V2.ProtocolRequestCounter")!;

    private static readonly MethodInfo protocolRequestCounterIncrement =
        protocolRequestCounterType.GetMethod("Increment", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly Type licenseExpirationCheckerType = typeof(IEndpointRouter).Assembly
        .GetType("Duende.IdentityServer.Licensing.V2.LicenseExpirationChecker")!;

    private static readonly MethodInfo licenseExpirationCheckerCheckExpiration =
        licenseExpirationCheckerType.GetMethod("CheckExpiration", BindingFlags.Public | BindingFlags.Instance)!;

    object? requestCounter;
    object? licenseExpirationChecker;

    private void ResolveServices(HttpContext context)
    {
        using var activity = EndpointActivitySource.StartActivity("Endpoint.ResolveServices");
        requestCounter = context.RequestServices.GetRequiredService(protocolRequestCounterType);
        licenseExpirationChecker = context.RequestServices.GetRequiredService(licenseExpirationCheckerType);
    }

    private void RequestCounterIncrement(HttpContext context)
    {
        using var activity = EndpointActivitySource.StartActivity("Endpoint.RequestCounter.Increment");

        protocolRequestCounterIncrement.Invoke(requestCounter, null);
    }

    private void LogDebug(string message, params object?[] args)
    {
        using var activity = EndpointActivitySource.StartActivity("Endpoint.LogDebug");

        logger.LogDebug(message, args);
    }

    private void LogWarning(string message, params object?[] args)
    {
        using var activity = EndpointActivitySource.StartActivity("Endpoint.LogWarning");

        logger.LogWarning(message, args);
    }

    private void CheckExpiration()
    {
        using var activity = EndpointActivitySource.StartActivity("Endpoint.CheckExpiration");

        licenseExpirationCheckerCheckExpiration.Invoke(licenseExpirationChecker, []);
    }

    public IEndpointHandler? Find(HttpContext context)
    {
        ResolveServices(context);

        using var activity = EndpointActivitySource.StartActivity("Endpoint.Find");

        ArgumentNullException.ThrowIfNull(context);

        foreach (var endpoint in endpoints)
        {
            var path = endpoint.Path;
            if (context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
            {
                var endpointName = endpoint.Name;
                //logger.LogDebug("Request path {path} matched to endpoint type {endpoint}", context.Request.Path, endpointName);
                LogDebug("Request path {path} matched to endpoint type {endpoint}", context.Request.Path, endpointName);

                //requestCounter.Increment();
                RequestCounterIncrement(context);

                //licenseExpirationChecker.CheckExpiration();
                CheckExpiration();

                return GetEndpointHandler(endpoint, context);
            }
        }

        //logger.LogTrace("No endpoint entry found for request path: {path}", context.Request.Path);
        LogDebug("No endpoint entry found for request path: {path}", context.Request.Path);

        return null;
    }

    private IEndpointHandler? GetEndpointHandler(Duende.IdentityServer.Hosting.Endpoint endpoint, HttpContext context)
    {
        using var activity = EndpointActivitySource.StartActivity("Endpoint.GetEndpointHandler");

        if (options.Endpoints.IsEndpointEnabled(endpoint))
        {
            if (context.RequestServices.GetService(endpoint.Handler) is IEndpointHandler handler)
            {
                //logger.LogDebug("Endpoint enabled: {endpoint}, successfully created handler: {endpointHandler}", endpoint.Name, endpoint.Handler.FullName);
                LogDebug("Endpoint enabled: {endpoint}, successfully created handler: {endpointHandler}", endpoint.Name, endpoint.Handler.FullName);
                return handler;
            }

            //logger.LogDebug("Endpoint enabled: {endpoint}, failed to create handler: {endpointHandler}", endpoint.Name, endpoint.Handler.FullName);
            LogDebug("Endpoint enabled: {endpoint}, failed to create handler: {endpointHandler}", endpoint.Name, endpoint.Handler.FullName);
        }
        else
        {
            //logger.LogWarning("Endpoint disabled: {endpoint}", endpoint.Name);
            LogWarning("Endpoint disabled: {endpoint}", endpoint.Name);
        }

        return null;
    }
}

internal static class EndpointOptionsExtensions
{
    public static bool IsEndpointEnabled(this EndpointsOptions options, Duende.IdentityServer.Hosting.Endpoint endpoint) => endpoint?.Name switch
    {
        IdentityServerConstants.EndpointNames.Authorize => options.EnableAuthorizeEndpoint,
        IdentityServerConstants.EndpointNames.CheckSession => options.EnableCheckSessionEndpoint,
        IdentityServerConstants.EndpointNames.DeviceAuthorization => options.EnableDeviceAuthorizationEndpoint,
        IdentityServerConstants.EndpointNames.Discovery => options.EnableDiscoveryEndpoint,
        IdentityServerConstants.EndpointNames.EndSession => options.EnableEndSessionEndpoint,
        IdentityServerConstants.EndpointNames.Introspection => options.EnableIntrospectionEndpoint,
        IdentityServerConstants.EndpointNames.Revocation => options.EnableTokenRevocationEndpoint,
        IdentityServerConstants.EndpointNames.Token => options.EnableTokenEndpoint,
        IdentityServerConstants.EndpointNames.UserInfo => options.EnableUserInfoEndpoint,
        IdentityServerConstants.EndpointNames.PushedAuthorization => options.EnablePushedAuthorizationEndpoint,
        IdentityServerConstants.EndpointNames.BackchannelAuthentication => options.EnableBackchannelAuthenticationEndpoint,
        _ => true
    };

    public static IServiceCollection AddOtelEndpointRouter(this IServiceCollection services)
    {
        services.RemoveAll<IEndpointRouter>();
        services.AddTransient<IEndpointRouter, OTelEndpointRouter>();

        services.AddOpenTelemetry()
            .WithTracing(t => t.AddSource(OTelEndpointRouter.TraceName));

        return services;
    }
}
