// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Services;

namespace Duende.IdentityServer.Conformance.Host;

/// <summary>
/// Custom backchannel logout HTTP client that rewrites URLs from the external
/// hostname (localhost) to the internal Docker hostname (nginx) so that
/// IdentityServer can reach the conformance suite from inside the container.
/// </summary>
internal sealed partial class ConformanceBackChannelLogoutHttpClient : IBackChannelLogoutHttpClient
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
            LogRewrittenUrl(_logger, url, internalUrl);
        }

        try
        {
            using var formEncodedContent = new FormUrlEncodedContent(payload);
            using var response = await _client.PostAsync(internalUrl, formEncodedContent, ct);
            if (response.IsSuccessStatusCode)
            {
                LogSuccess(_logger, internalUrl, (int)response.StatusCode);
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    LogFailure(_logger, internalUrl, (int)response.StatusCode, body);
                }
            }
        }
        catch (Exception ex)
        {
            LogException(_logger, internalUrl, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rewrote backchannel logout URL from {ExternalUrl} to {InternalUrl}")]
    private static partial void LogRewrittenUrl(ILogger logger, string externalUrl, string internalUrl);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Backchannel logout succeeded for {Url}, status: {Status}")]
    private static partial void LogSuccess(ILogger logger, string url, int status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Backchannel logout failed for {Url}, status: {Status}, body: {Body}")]
    private static partial void LogFailure(ILogger logger, string url, int status, string body);

    [LoggerMessage(Level = LogLevel.Error, Message = "Exception invoking backchannel logout for {Url}")]
    private static partial void LogException(ILogger logger, string url, Exception exception);
}
