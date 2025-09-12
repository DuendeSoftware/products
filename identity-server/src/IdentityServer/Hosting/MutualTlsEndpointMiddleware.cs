// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Logging;
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
    public const string OriginalPath = "Duende.IdentityServer.MutualTlsEndpointMiddleware.OriginalPath";

    private readonly SanitizedLogger<MutualTlsEndpointMiddleware> _sanitizedLogger;
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
        _sanitizedLogger = new SanitizedLogger<MutualTlsEndpointMiddleware>(logger);
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
            if (_options.MutualTls.DomainName.Contains('.', StringComparison.InvariantCulture))
            {
                _ = HostString.FromUriComponent(_options.MutualTls.DomainName);
                // Separate domain
                if (RequestedHostMatches(context.Request.Host, _options.MutualTls.DomainName))
                {
                    _sanitizedLogger.LogDebug("Requiring mTLS because the request's domain matches the configured mTLS domain name.");
                    return MtlsEndpointType.SeparateDomain;
                }
            }
            else
            {
                // Subdomain
                if (context.Request.Host.Host.StartsWith(_options.MutualTls.DomainName + ".", StringComparison.OrdinalIgnoreCase))
                {
                    _sanitizedLogger.LogDebug("Requiring mTLS because the request's subdomain matches the configured mTLS domain name.");
                    return MtlsEndpointType.Subdomain;
                }
            }

            _sanitizedLogger.LogDebug("Not requiring mTLS because this request's domain does not match the configured mTLS domain name.");
            return MtlsEndpointType.None;
        }

        // Check path-based MTLS
        if (context.Request.Path.StartsWithSegments(
            ProtocolRoutePaths.MtlsPathPrefix.EnsureLeadingSlash(), out var path))
        {
            _sanitizedLogger.LogDebug("Requiring mTLS because the request's path begins with the configured mTLS path prefix.");
            subPath = path;
            return MtlsEndpointType.PathBased;
        }

        return MtlsEndpointType.None;
    }

    /// <inheritdoc />
#pragma warning disable IDE0060 // Remove unused parameter
    public async Task Invoke(HttpContext context, IAuthenticationSchemeProvider schemes)
#pragma warning restore IDE0060 // Remove unused parameter
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

                _sanitizedLogger.LogDebug("Rewriting MTLS request from: {oldPath} to: {newPath}",
                    context.Request.Path.ToString(), path);

                // Capture the original path before any modifications. This is useful in other parts of the
                // pipeline, that may want to include this context.
                context.Items[OriginalPath] = context.Request.Path;

                context.Request.Path = path;
            }
        }

        await _next(context);
    }


    private static bool RequestedHostMatches(HostString requestHost, string configuredDomain)
    {
        // Parse the configured domain which might contain a port
        var configuredHostname = configuredDomain;
        var configuredPort = 443;

        var colonIndex = configuredDomain.IndexOf(':', StringComparison.InvariantCulture);
        if (colonIndex >= 0)
        {
            configuredHostname = configuredDomain[..colonIndex];
            if (int.TryParse(configuredDomain.AsSpan(colonIndex + 1), out var port))
            {
                configuredPort = port;
            }
        }

        // Compare hostnames (case-insensitive)
        if (!string.Equals(requestHost.Host, configuredHostname, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestPort = requestHost.Port ?? 443;
        return requestPort == configuredPort;
    }

    private async Task<AuthenticateResult> TriggerCertificateAuthentication(HttpContext context)
    {
        var x509AuthResult = await context.AuthenticateAsync(_options.MutualTls.ClientCertificateAuthenticationScheme);

        if (!x509AuthResult.Succeeded)
        {
            _sanitizedLogger.LogDebug("MTLS authentication failed, error: {error}.", x509AuthResult.Failure?.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteJsonAsync(new MtlsErrorResponse
            {
                error = "invalid_client",
                error_description = "mTLS authentication failed."
            });
        }

        return x509AuthResult;
    }

    private class MtlsErrorResponse
    {
        public string error { get; set; }
        public string error_description { get; set; }
    }

}
