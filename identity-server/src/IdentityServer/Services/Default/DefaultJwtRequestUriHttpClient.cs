// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Logging;
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
    private readonly SanitizedLogger<DefaultJwtRequestUriHttpClient> _sanitizedLogger;
    private readonly ICancellationTokenProvider _cancellationTokenProvider;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="client">An HTTP client</param>
    /// <param name="options">The options.</param>
    /// <param name="loggerFactory">The logger factory</param>
    /// <param name="cancellationTokenProvider"></param>
    public DefaultJwtRequestUriHttpClient(HttpClient client, IdentityServerOptions options,
        ILoggerFactory loggerFactory, ICancellationTokenProvider cancellationTokenProvider)
    {
        _client = client;
        _options = options;
        _sanitizedLogger = new SanitizedLogger<DefaultJwtRequestUriHttpClient>(loggerFactory.CreateLogger<DefaultJwtRequestUriHttpClient>());
        _cancellationTokenProvider = cancellationTokenProvider;
    }


    /// <inheritdoc />
    public async Task<string> GetJwtAsync(string url, Client client)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultJwtRequestUriHttpClient.GetJwt");

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Options.TryAdd(IdentityServerConstants.JwtRequestClientKey, client);

        var response = await _client.SendAsync(req, _cancellationTokenProvider.CancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            if (_options.StrictJarValidation)
            {
                if (!string.Equals(response.Content.Headers.ContentType.MediaType,
                        $"application/{JwtClaimTypes.JwtTypes.AuthorizationRequest}", StringComparison.Ordinal))
                {
                    _sanitizedLogger.LogError("Invalid content type {type} from jwt url {url}",
                        response.Content.Headers.ContentType.MediaType, url.ReplaceLineEndings(string.Empty));
                    return null;
                }
            }

            _sanitizedLogger.LogDebug("Success http response from jwt url {url}", url);

            var json = await response.Content.ReadAsStringAsync();
            return json;
        }

        _sanitizedLogger.LogError("Invalid http status code {status} from jwt url {url}", response.StatusCode, url);
        return null;
    }
}
