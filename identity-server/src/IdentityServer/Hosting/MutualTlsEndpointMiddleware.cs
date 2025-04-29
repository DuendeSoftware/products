// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Threading.Tasks;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Hosting;

/// <summary>
///     Middleware for re-writing the MTLS enabled endpoints to the standard protocol endpoints
/// </summary>
public class MutualTlsEndpointMiddleware
{
    private readonly ILogger<MutualTlsEndpointMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly IdentityServerOptions _options;

    /// <summary>
    ///     ctor
    /// </summary>
    /// <param name="next"></param>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    public MutualTlsEndpointMiddleware(RequestDelegate next, IdentityServerOptions options,
        ILogger<MutualTlsEndpointMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    internal enum MtlsEndpointType
    {
        None,
        SeparateDomain,
        Subdomain,
        PathBased
    }

    internal MtlsEndpointType DetermineMtlsEndpointType(HttpContext context, out PathString? subPath)
    {
        subPath = null;

        if (!_options.MutualTls.Enabled)
        {
            return MtlsEndpointType.None;
        }

        if (_options.MutualTls.DomainName.IsPresent())
        {
            if (_options.MutualTls.DomainName.Contains('.'))
            {
                // Separate domain
                if (context.Request.Host.Host.Equals(_options.MutualTls.DomainName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Requiring mTLS because the request's domain matches the configured mTLS domain name.");
                    return MtlsEndpointType.SeparateDomain;
                }
            }
            else
            {
                // Subdomain
                if (context.Request.Host.Host.StartsWith(_options.MutualTls.DomainName + ".", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Requiring mTLS because the request's subdomain matches the configured mTLS domain name.");
                    return MtlsEndpointType.Subdomain;
                }
            }

            _logger.LogDebug("Not requiring mTLS because this request's domain does not match the configured mTLS domain name.");
            return MtlsEndpointType.None;
        }

        // Check path-based MTLS
        if (context.Request.Path.StartsWithSegments(
            ProtocolRoutePaths.MtlsPathPrefix.EnsureLeadingSlash(), out var path))
        {
            _logger.LogDebug("Requiring mTLS because the request's path begins with the configured mTLS path prefix.");
            subPath = path;
            return MtlsEndpointType.PathBased;
        }

        return MtlsEndpointType.None;
    }

    /// <inheritdoc />
    public async Task Invoke(HttpContext context, IAuthenticationSchemeProvider schemes)
    {
        var mtlsConfigurationStyle = DetermineMtlsEndpointType(context, out var subPath);

        if (mtlsConfigurationStyle != MtlsEndpointType.None)
        {
            var result = await TriggerCertificateAuthentication(context);
            if (!result.Succeeded)
            {
                return;
            }

            // Additional processing for path-based MTLS
            if (mtlsConfigurationStyle == MtlsEndpointType.PathBased && subPath.HasValue)
            {
                var path = ProtocolRoutePaths.ConnectPathPrefix + subPath.Value.ToString().EnsureLeadingSlash();
                path = path.EnsureLeadingSlash();

                _logger.LogDebug("Rewriting MTLS request from: {oldPath} to: {newPath}",
                    context.Request.Path.ToString(), path);
                context.Request.Path = path;
            }
        }

        await _next(context);
    }

    private async Task<AuthenticateResult> TriggerCertificateAuthentication(HttpContext context)
    {
        var x509AuthResult =
            await context.AuthenticateAsync(_options.MutualTls.ClientCertificateAuthenticationScheme);

        if (!x509AuthResult.Succeeded)
        {
            _logger.LogDebug("MTLS authentication failed, error: {error}.",
                x509AuthResult.Failure?.Message);
            await context.ForbidAsync(_options.MutualTls.ClientCertificateAuthenticationScheme);
        }

        return x509AuthResult;
    }
}