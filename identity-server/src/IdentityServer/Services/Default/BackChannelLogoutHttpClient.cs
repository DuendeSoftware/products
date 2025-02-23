// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Models making HTTP requests for back-channel logout notification.
/// </summary>
public class DefaultBackChannelLogoutHttpClient : IBackChannelLogoutHttpClient
{
    private readonly HttpClient _client;
    private readonly ILogger<DefaultBackChannelLogoutHttpClient> _logger;
    private readonly ICancellationTokenProvider _cancellationTokenProvider;

    /// <summary>
    /// Constructor for BackChannelLogoutHttpClient.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="cancellationTokenProvider"></param>
    public DefaultBackChannelLogoutHttpClient(HttpClient client, ILoggerFactory loggerFactory, ICancellationTokenProvider cancellationTokenProvider)
    {
        _client = client;
        _logger = loggerFactory.CreateLogger<DefaultBackChannelLogoutHttpClient>();
        _cancellationTokenProvider = cancellationTokenProvider;
    }

    /// <summary>
    /// Posts the payload to the url.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="payload"></param>
    /// <returns></returns>
    public async Task PostAsync(string url, Dictionary<string, string> payload)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultBackChannelLogoutHttpClient.Post");
        
        try
        {
            var response = await _client.PostAsync(url, new FormUrlEncodedContent(payload), _cancellationTokenProvider.CancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Response from back-channel logout endpoint: {url} status code: {status}", url, (int) response.StatusCode);
            }
            else
            {
                BackChannelError err = null;
                
                var errorjson = await response.Content.ReadAsStringAsync();
                try
                {
                    err = JsonSerializer.Deserialize<BackChannelError>(errorjson);
                }
                catch { }

                if (err == null)
                {
                    _logger.LogWarning("Response from back-channel logout endpoint: {url} status code: {status}", url, (int) response.StatusCode);
                }
                else
                {
                    _logger.LogWarning("Response from back-channel logout endpoint: {url} status code: {status}, error: {error}, error_description: {error_description}", url, (int) response.StatusCode, err.error, err.error_description);
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