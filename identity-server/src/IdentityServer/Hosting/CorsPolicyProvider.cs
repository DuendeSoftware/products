// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.DependencyInjection;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting;

internal class CorsPolicyProvider : ICorsPolicyProvider
{
    private readonly ILogger<CorsPolicyProvider> _logger;
    private readonly ICorsPolicyProvider _inner;
    private readonly IServiceProvider _provider;
    private readonly IdentityServerOptions _options;

    public CorsPolicyProvider(
        ILogger<CorsPolicyProvider> logger,
        Decorator<ICorsPolicyProvider> inner,
        IdentityServerOptions options,
        IServiceProvider provider)
    {
        _logger = logger;
        _inner = inner.Instance;
        _options = options;
        _provider = provider;
    }

    public Task<CorsPolicy> GetPolicyAsync(HttpContext context, string policyName)
    {
        if (_options.Cors.CorsPolicyName == policyName)
        {
            return ProcessAsync(context);
        }
        else
        {
            return _inner.GetPolicyAsync(context, policyName);
        }
    }

    private async Task<CorsPolicy> ProcessAsync(HttpContext context)
    {
        var origin = context.Request.GetCorsOrigin();
        if (origin != null)
        {
            var path = context.Request.Path;
            if (IsPathAllowed(path))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.CORSRequestMadeForPathPathFromOrigin(
                        path.ToString().SanitizeLogParameter(),
                        origin.SanitizeLogParameter());
                }

                // manually resolving this from DI because this: 
                // https://github.com/aspnet/CORS/issues/105
                var corsPolicyService = _provider.GetRequiredService<ICorsPolicyService>();

                if (await corsPolicyService.IsOriginAllowedAsync(origin, context.RequestAborted))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.CorsPolicyServiceAllowedOriginOrigin(origin.SanitizeLogParameter());
                    }
                    return Allow(origin);
                }
                else
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        _logger.CorsPolicyServiceDidNotAllowOriginOrigin(origin.SanitizeLogParameter());
                    }
                }
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.IdentityServerCorsPolicyServiceDidnTHandleCORSRequestMade(
                        path.ToString().SanitizeLogParameter(),
                        origin.SanitizeLogParameter());
                }
            }
        }

        return null;
    }

    private CorsPolicy Allow(string origin)
    {
        var policyBuilder = new CorsPolicyBuilder()
            .WithOrigins(origin)
            .AllowAnyHeader()
            .AllowAnyMethod();

        if (_options.Cors.PreflightCacheDuration.HasValue)
        {
            policyBuilder.SetPreflightMaxAge(_options.Cors.PreflightCacheDuration.Value);
        }

        return policyBuilder.Build();
    }

    private bool IsPathAllowed(PathString path) => _options.Cors.CorsPaths.Any(x => path == x);
}
