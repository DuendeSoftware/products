// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default JwtRequest client
/// </summary>
public class DefaultJwtRequestUriHttpClient : IJwtRequestUriHttpClient
{
    private readonly HttpClient _client;
    private readonly IdentityServerOptions _options;
    private readonly ILogger<DefaultJwtRequestUriHttpClient> _logger;

    internal DefaultJwtRequestUriHttpClient(HttpClient client, IdentityServerOptions options,
        ILogger<DefaultJwtRequestUriHttpClient> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="client">An HTTP client</param>
    /// <param name="options">The options.</param>
    /// <param name="loggerFactory">The logger factory</param>
    public DefaultJwtRequestUriHttpClient(HttpClient client, IdentityServerOptions options,
        ILoggerFactory loggerFactory)
        : this(client, options, loggerFactory.CreateLogger<DefaultJwtRequestUriHttpClient>())
    {
    }


    /// <inheritdoc />
    public async Task<string> GetJwtAsync(string url, Client client, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultJwtRequestUriHttpClient.GetJwt");

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Options.TryAdd(IdentityServerConstants.JwtRequestClientKey, client);

        var response = await _client.SendAsync(req, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            if (_options.StrictJarValidation)
            {
                if (!string.Equals(response.Content.Headers.ContentType.MediaType,
                        $"application/{JwtClaimTypes.JwtTypes.AuthorizationRequest}", StringComparison.Ordinal))
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.InvalidContentTypeTypeFromJwtUrlUrl(
                            response.Content.Headers.ContentType.MediaType.SanitizeLogParameter(),
                            url.SanitizeLogParameter());
                    }
                    return null;
                }
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.SuccessHttpResponseFromJwtUrlUrl(url.SanitizeLogParameter());
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            return json;
        }

        if (_logger.IsEnabled(LogLevel.Error))
        {
            _logger.InvalidHttpStatusCodeStatusFromJwtUrl(
                response.StatusCode,
                url.SanitizeLogParameter());
        }
        return null;
    }
}
