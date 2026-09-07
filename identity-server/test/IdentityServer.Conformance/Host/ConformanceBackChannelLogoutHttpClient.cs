// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Services;

namespace Duende.IdentityServer.Conformance.Host;

/// <summary>
/// Custom backchannel logout HTTP client that rewrites URLs from the external
/// hostname (localhost) to the internal Docker hostname (nginx) so that
/// IdentityServer can reach the conformance suite from inside the container.
/// </summary>
internal sealed class ConformanceBackChannelLogoutHttpClient : IBackChannelLogoutHttpClient
{
    private readonly HttpClient _client;
    private readonly ILogger<ConformanceBackChannelLogoutHttpClient> _logger;
    private readonly string _externalHost;
    private readonly string _internalHost;

    public ConformanceBackChannelLogoutHttpClient(
        HttpClient client,
        ILogger<ConformanceBackChannelLogoutHttpClient> logger,
        string externalHost = "localhost:8443",
        string internalHost = "nginx:8443")
    {
        _client = client;
        _logger = logger;
        _externalHost = externalHost;
        _internalHost = internalHost;
    }

    public async Task PostAsync(string url, Dictionary<string, string> payload, CancellationToken ct)
    {
        var internalUrl = url.Replace(
            $"https://{_externalHost}", $"https://{_internalHost}", StringComparison.OrdinalIgnoreCase);

        if (internalUrl != url)
        {
            _logger.LogDebug("Rewrote backchannel logout URL from {ExternalUrl} to {InternalUrl}", url, internalUrl);
        }

        try
        {
            using var formEncodedContent = new FormUrlEncodedContent(payload);
            var response = await _client.PostAsync(internalUrl, formEncodedContent, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Backchannel logout succeeded for {Url}, status: {Status}", internalUrl, (int)response.StatusCode);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Backchannel logout failed for {Url}, status: {Status}, body: {Body}", internalUrl, (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception invoking backchannel logout for {Url}", internalUrl);
        }
    }
}
