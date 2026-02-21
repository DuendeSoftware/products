// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.DependencyInjection;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Logging;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Hosting;

internal class CorsPolicyProvider : ICorsPolicyProvider
{
    private readonly SanitizedLogger<CorsPolicyProvider> _sanitizedLogger;
    private readonly ICorsPolicyProvider _inner;
    private readonly IServiceProvider _provider;
    private readonly IdentityServerOptions _options;

    public CorsPolicyProvider(
        SanitizedLogger<CorsPolicyProvider> sanitizedLogger,
        Decorator<ICorsPolicyProvider> inner,
        IdentityServerOptions options,
        IServiceProvider provider)
    {
        _sanitizedLogger = sanitizedLogger;
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
                _sanitizedLogger.LogDebug("CORS request made for path: {path} from origin: {origin}", path, origin);

                // manually resolving this from DI because this: 
                // https://github.com/aspnet/CORS/issues/105
                var corsPolicyService = _provider.GetRequiredService<ICorsPolicyService>();

                if (await corsPolicyService.IsOriginAllowedAsync(origin, context.RequestAborted))
                {
                    _sanitizedLogger.LogDebug("CorsPolicyService allowed origin: {origin}", origin);
                    return Allow(origin);
                }
                else
                {
                    _sanitizedLogger.LogWarning("CorsPolicyService did not allow origin: {origin}", origin);
                }
            }
            else
            {
                _sanitizedLogger.LogDebug("IdentityServer CorsPolicyService didn't handle CORS request made for path: {path} from origin: {origin} " +
                                          "because it is not for an IdentityServer CORS endpoint. To allow CORS requests to non IdentityServer endpoints, please " +
                                          "set up your own Cors policy for your application by calling app.UseCors(\"MyPolicy\") in the pipeline setup.", path, origin);
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
