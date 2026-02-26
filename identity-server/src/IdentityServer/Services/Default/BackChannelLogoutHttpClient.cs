// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Models making HTTP requests for back-channel logout notification.
/// </summary>
public class DefaultBackChannelLogoutHttpClient : IBackChannelLogoutHttpClient
{
    private readonly HttpClient _client;
    private readonly ILogger<DefaultBackChannelLogoutHttpClient> _logger;

    /// <summary>
    /// Constructor for BackChannelLogoutHttpClient.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="loggerFactory"></param>
    public DefaultBackChannelLogoutHttpClient(HttpClient client, ILoggerFactory loggerFactory)
    {
        _client = client;
        _logger = loggerFactory.CreateLogger<DefaultBackChannelLogoutHttpClient>();
    }

    /// <summary>
    /// Posts the payload to the url.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="payload"></param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    public async Task PostAsync(string url, Dictionary<string, string> payload, Ct ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultBackChannelLogoutHttpClient.Post");

        try
        {
            using var formEncodedContent = new FormUrlEncodedContent(payload);
            var response = await _client.PostAsync(url, formEncodedContent, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Response from back-channel logout endpoint: {url} status code: {status}", url, (int)response.StatusCode);
            }
            else
            {
                BackChannelError err = null;

                var errorjson = await response.Content.ReadAsStringAsync(ct);
                try
                {
                    err = JsonSerializer.Deserialize<BackChannelError>(errorjson);
                }
                catch { }

                if (err == null)
                {
                    _logger.LogWarning("Response from back-channel logout endpoint: {url} status code: {status}", url, (int)response.StatusCode);
                }
                else
                {
                    _logger.LogWarning("Response from back-channel logout endpoint: {url} status code: {status}, error: {error}, error_description: {error_description}", url, (int)response.StatusCode, err.error, err.error_description);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception invoking back-channel logout for url: {url}", url);
        }
    }

    internal class BackChannelError
    {
        public string error { get; set; }
        public string error_description { get; set; }
    }
}
