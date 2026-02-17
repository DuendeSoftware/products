// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default back-channel logout notification implementation.
/// </summary>
public class DefaultBackChannelLogoutService : IBackChannelLogoutService
{
    /// <summary>
    /// Default value for the back-channel JWT lifetime.
    /// </summary>
    protected const int DefaultLogoutTokenLifetime = 5 * 60;

    /// <summary>
    /// The system clock;
    /// </summary>
    protected TimeProvider TimeProvider { get; }

    /// <summary>
    /// The IdentityServerTools used to create the JWT.
    /// </summary>
    protected IIdentityServerTools Tools { get; }

    /// <summary>
    /// The ILogoutNotificationService to build the back channel logout requests.
    /// </summary>
    public ILogoutNotificationService LogoutNotificationService { get; }

    /// <summary>
    /// HttpClient to make the outbound HTTP calls.
    /// </summary>
    protected IBackChannelLogoutHttpClient HttpClient { get; }

    /// <summary>
    /// The logger.
    /// </summary>
    protected ILogger<IBackChannelLogoutService> Logger { get; }

    /// <summary>
    /// Ths issuer name service.
    /// </summary>
    protected IIssuerNameService IssuerNameService { get; }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="timeProvider"></param>
    /// <param name="tools"></param>
    /// <param name="logoutNotificationService"></param>
    /// <param name="backChannelLogoutHttpClient"></param>
    /// <param name="issuerNameService"></param>
    /// <param name="logger"></param>
    public DefaultBackChannelLogoutService(
        TimeProvider timeProvider,
        IIdentityServerTools tools,
        ILogoutNotificationService logoutNotificationService,
        IBackChannelLogoutHttpClient backChannelLogoutHttpClient,
        IIssuerNameService issuerNameService,
        ILogger<IBackChannelLogoutService> logger)
    {
        TimeProvider = timeProvider;
        Tools = tools;
        LogoutNotificationService = logoutNotificationService;
        HttpClient = backChannelLogoutHttpClient;
        Logger = logger;
        IssuerNameService = issuerNameService;
    }

    /// <inheritdoc/>
    public virtual async Task SendLogoutNotificationsAsync(LogoutNotificationContext context)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultBackChannelLogoutService.SendLogoutNotifications");

        var backChannelRequests = await LogoutNotificationService.GetBackChannelLogoutNotificationsAsync(context);
        if (backChannelRequests.Any())
        {
            await SendLogoutNotificationsAsync(backChannelRequests);
        }
    }

    /// <summary>
    /// Sends the logout notifications for the collection of clients.
    /// </summary>
    /// <param name="requests"></param>
    /// <returns></returns>
    protected virtual async Task SendLogoutNotificationsAsync(IEnumerable<BackChannelLogoutRequest> requests)
    {
        requests ??= [];
        var logoutRequestsWithPayload = new List<(BackChannelLogoutRequest, Dictionary<string, string>)>();
        foreach (var backChannelLogoutRequest in requests)
        {
            // Creation of the payload can cause database access to retrieve the
            // signing key. That needs to be done in serial so that our EF store
            // implementation doesn't make parallel use of a single DB context.
            // Since the signing key material should be cached, only the
            // first serial operation will call the db.
            var payload = await CreateFormPostPayloadAsync(backChannelLogoutRequest);
            logoutRequestsWithPayload.Add((backChannelLogoutRequest, payload));
        }

        var logoutRequests = logoutRequestsWithPayload.Select(request => PostLogoutJwt(request.Item1, request.Item2)).ToArray();
        await Task.WhenAll(logoutRequests);
    }

    /// <summary>
    /// Performs the HTTP POST of the logout payload to the client.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    protected virtual Task PostLogoutJwt(BackChannelLogoutRequest client, Dictionary<string, string> data) => HttpClient.PostAsync(client.LogoutUri, data);

    /// <summary>
    /// Creates the form-url-encoded payload (as a dictionary) to send to the client.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    protected async Task<Dictionary<string, string>> CreateFormPostPayloadAsync(BackChannelLogoutRequest request)
    {
        var token = await CreateTokenAsync(request);

        var data = new Dictionary<string, string>
        {
            { OidcConstants.BackChannelLogoutRequest.LogoutToken, token }
        };
        return data;
    }

    /// <summary>
    /// Creates the JWT used for the back-channel logout notification.
    /// </summary>
    /// <param name="request"></param>
    /// <returns>The token.</returns>
    protected virtual async Task<string> CreateTokenAsync(BackChannelLogoutRequest request)
    {
        var claims = await CreateClaimsForTokenAsync(request);
        if (claims.Any(x => x.Type == JwtClaimTypes.Nonce))
        {
            throw new InvalidOperationException("nonce claim is not allowed in the back-channel signout token.");
        }

        if (request.Issuer != null)
        {
            return await Tools.IssueJwtAsync(DefaultLogoutTokenLifetime, request.Issuer, IdentityServerConstants.TokenTypes.LogoutToken, claims);
        }

        var issuer = await IssuerNameService.GetCurrentAsync();
        return await Tools.IssueJwtAsync(DefaultLogoutTokenLifetime, issuer, IdentityServerConstants.TokenTypes.LogoutToken, claims);
    }

    /// <summary>
    /// Create the claims to be used in the back-channel logout token.
    /// </summary>
    /// <param name="request"></param>
    /// <returns>The claims to include in the token.</returns>
#pragma warning disable CA1822 // Changing this on a protected method in a public class would be a breaking change.
    protected Task<IEnumerable<Claim>> CreateClaimsForTokenAsync(BackChannelLogoutRequest request)
#pragma warning restore CA1822
    {
        if (request.SessionIdRequired && request.SessionId == null)
        {
            throw new ArgumentException("Client requires SessionId", nameof(request.SessionId));
        }
        if (request.SubjectId == null && request.SessionId == null)
        {
            throw new ArgumentException("Either a SubjectId or SessionId is required.");
        }

        var json = "{\"" + OidcConstants.Events.BackChannelLogout + "\":{} }";

        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Audience, request.ClientId),
            new Claim(JwtClaimTypes.JwtId, CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Hex)),
            new Claim(JwtClaimTypes.Events, json, IdentityServerConstants.ClaimValueTypes.Json)
        };

        if (request.SubjectId != null)
        {
            claims.Add(new Claim(JwtClaimTypes.Subject, request.SubjectId));
        }

        if (request.SessionId != null)
        {
            claims.Add(new Claim(JwtClaimTypes.SessionId, request.SessionId));
        }

        var reason = request.LogoutReason switch
        {
            LogoutNotificationReason.UserLogout => IdentityServerConstants.BackChannelLogoutReasons.UserLogout,
            LogoutNotificationReason.SessionExpiration => IdentityServerConstants.BackChannelLogoutReasons.SessionExpiration,
            LogoutNotificationReason.Terminated => IdentityServerConstants.BackChannelLogoutReasons.Terminated,
            _ => null,
        };
        if (reason != null)
        {
            claims.Add(new Claim(IdentityServerConstants.ClaimTypes.BackChannelLogoutReason, reason));
        }

        return Task.FromResult(claims.AsEnumerable());
    }
}
