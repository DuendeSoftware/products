// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Endpoints.Results;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Endpoints.Results;

/// <summary>
/// Result when SAML Single Logout requires the user to be logged out via the IdentityServer logout page.
/// </summary>
/// <param name="validatedLogoutRequest">The validated logout request context</param>
public class Saml2LogoutPageResult(ValidatedLogoutRequest validatedLogoutRequest)
    : EndpointResult<Saml2LogoutPageResult>
{
    /// <summary>
    /// The validated SAML logout request.
    /// </summary>
    public ValidatedLogoutRequest Request { get; } = validatedLogoutRequest ?? throw new ArgumentNullException(nameof(validatedLogoutRequest));
}

/// <summary>
/// Response writer for redirecting to the logout page, persisting SAML logout state
/// and passing the state identifier through the logoutId query string parameter.
/// </summary>
internal sealed class Saml2LogoutPageResultHttpWriter(
    IUserSession userSession,
    IMessageStore<LogoutMessage> logoutMessageStore,
    IServerUrls serverUrls,
    TimeProvider timeProvider,
    IOptions<IdentityServerOptions> identityServerOptions)
    : IHttpResponseWriter<Saml2LogoutPageResult>
{
    /// <inheritdoc/>
    public async Task WriteHttpResponse(Saml2LogoutPageResult result, HttpContext context)
    {
        var request = result.Request;
        var ct = context.RequestAborted;

        var user = await userSession.GetUserAsync(ct);
        var samlSessions = await userSession.GetSamlSessionListAsync(ct);
        var oidcClientIds = await userSession.GetClientListAsync(ct);
        var sessionId = await userSession.GetSessionIdAsync(ct);

        var callbackPath = identityServerOptions.Value.Saml.Endpoints.SingleLogoutCallbackPath;
        var callbackUrl = serverUrls.BasePath.EnsureTrailingSlash() + callbackPath.TrimStart('/');

        var logoutMessage = new LogoutMessage
        {
            SubjectId = user?.GetSubjectId(),
            SessionId = sessionId,
            ClientIds = oidcClientIds,
            SamlServiceProviderEntityId = request.Saml2Sp?.EntityId,
            SamlSessions = samlSessions,
            SamlLogoutRequestId = request.LogoutRequest.Id,
            SamlRelayState = request.RelayState ?? request.Saml2Message?.RelayState,
            PostLogoutRedirectUri = callbackUrl
        };

        var msg = new Message<LogoutMessage>(logoutMessage, timeProvider.GetUtcNow().UtcDateTime);
        var logoutId = await logoutMessageStore.WriteAsync(msg, ct);

        var logoutUrl = identityServerOptions.Value.UserInteraction.LogoutUrl
            ?? throw new InvalidOperationException("LogoutUrl is not configured in IdentityServerOptions.UserInteraction");
        if (logoutUrl.IsLocalUrl())
        {
            logoutUrl = serverUrls.GetIdentityServerRelativeUrl(logoutUrl);
        }

        logoutUrl = logoutUrl.AddQueryString(identityServerOptions.Value.UserInteraction.LogoutIdParameter, logoutId);

        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = logoutUrl;
    }
}
