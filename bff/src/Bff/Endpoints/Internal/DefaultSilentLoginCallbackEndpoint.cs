// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.Bff.Configuration;
using Duende.Bff.Otel;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints.Internal;

/// <summary>
/// Service for handling silent login callback requests
/// </summary>
internal class DefaultSilentLoginCallbackEndpoint(
    IOptions<BffOptions> options,
    ILogger<DefaultSilentLoginCallbackEndpoint> logger) : ISilentLoginCallbackEndpoint
{

    /// <inheritdoc />
    public async Task ProcessRequestAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        logger.ProcessingSilentLoginCallbackRequest(LogLevel.Debug);

        context.CheckForBffMiddleware(options.Value);

        var result = (await context.AuthenticateAsync()).Succeeded ? "true" : "false";
        var json = $"{{source:'bff-silent-login', isLoggedIn:{result}}}";

        var nonce = CryptoRandom.CreateUniqueId(format: CryptoRandom.OutputFormat.Hex);

        string origin;

        if (options.Value.AllowedSilentLoginReferers.Count == 0)
        {
            origin = $"{context.Request.Scheme}://{context.Request.Host}";
        }
        else if (!TryGetOriginFromReferer(context, out origin))
        {
            context.ReturnHttpProblem("Referer not allowed");
            return;
        }

        var html = $"<script nonce='{nonce}'>window.parent.postMessage({json}, '{origin}');</script>";

        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/html";

        context.Response.Headers["Content-Security-Policy"] = $"script-src 'nonce-{nonce}';";
        context.Response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0";
        context.Response.Headers["Pragma"] = "no-cache";

        logger.SilentLoginEndpointRenderingHtml(LogLevel.Debug, origin.Sanitize(), result);

        await context.Response.WriteAsync(html, Encoding.UTF8, cancellationToken);
    }

    private bool TryGetOriginFromReferer(HttpContext context, out string referer)
    {
        referer = null!;
        if (!context.Request.Headers.TryGetValue("Referer", out var refererValues))
        {
            logger.SilentLoginEndpointRefererHeaderMissing(LogLevel.Information);
            return false;
        }

        referer = refererValues.FirstOrDefault()?.TrimEnd('/') ?? string.Empty;
        if (!options.Value.AllowedSilentLoginReferers.Contains(referer, StringComparer.OrdinalIgnoreCase))
        {
            logger.SilentLoginEndpointRefererNotAllowed(LogLevel.Information, referer.Sanitize() ?? "", string.Join(", ", options.Value.AllowedSilentLoginReferers));
            return false;
        }

        return true;
    }
}
