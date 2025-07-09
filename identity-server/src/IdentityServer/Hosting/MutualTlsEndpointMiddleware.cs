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

    /// <inheritdoc />
    public async Task Invoke(HttpContext context, IAuthenticationSchemeProvider schemes)
    {
        if (_options.MutualTls.Enabled)
        {
            // domain-based MTLS
            if (_options.MutualTls.DomainName.IsPresent())
            {
                // separate domain
                if (_options.MutualTls.DomainName.Contains('.', StringComparison.InvariantCulture))
                {
                    if (context.Request.Host.Host.Equals(_options.MutualTls.DomainName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        var result = await TriggerCertificateAuthentication(context);
                        if (!result.Succeeded)
                        {
                            return;
                        }
                    }
                }
                // sub-domain
                else
                {
                    if (context.Request.Host.Host.StartsWith(_options.MutualTls.DomainName + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        var result = await TriggerCertificateAuthentication(context);
                        if (!result.Succeeded)
                        {
                            return;
                        }
                    }
                }
            }
            // path based MTLS
            else if (context.Request.Path.StartsWithSegments(ProtocolRoutePaths.MtlsPathPrefix.EnsureLeadingSlash(), out var subPath))
            {
                var result = await TriggerCertificateAuthentication(context);

                if (result.Succeeded)
                {
                    var path = ProtocolRoutePaths.ConnectPathPrefix + subPath.ToString().EnsureLeadingSlash();
                    path = path.EnsureLeadingSlash();

                    _sanitizedLogger.LogDebug("Rewriting MTLS request from: {oldPath} to: {newPath}",
                        context.Request.Path.ToString(), path);
                    context.Request.Path = path;
                }
                else
                {
                    return;
                }
            }
        }

        await _next(context);
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

    class MtlsErrorResponse
    {
        public string error { get; set; }
        public string error_description { get; set; }
    }

}
