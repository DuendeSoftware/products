// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.FederatedSignOut;

internal class AuthenticationRequestHandlerWrapper : IAuthenticationRequestHandler
{
    private static readonly CompositeFormat IframeHtml = CompositeFormat.Parse("<iframe style='display:none' width='0' height='0' src='{0}'></iframe>");

    private readonly IAuthenticationRequestHandler _inner;
    private readonly HttpContext _context;
    private readonly ILogger _logger;

    public AuthenticationRequestHandlerWrapper(IAuthenticationRequestHandler inner, IHttpContextAccessor httpContextAccessor)
    {
        _inner = inner;
        _context = httpContextAccessor.HttpContext;

        var factory = _context.RequestServices.GetService<ILoggerFactory>();
        _logger = factory?.CreateLogger(GetType());
    }

    public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context) => _inner.InitializeAsync(scheme, context);

    public async Task<bool> HandleRequestAsync()
    {
        var result = await _inner.HandleRequestAsync();

        if (result && _context.GetSignOutCalled())
        {
            // Check if this is a SAML2 IdP-initiated logout by looking for the context
            // set by the LogoutResponseCreated notification.
            if (_context.Items.TryGetValue(SamlSpLogoutContext.HttpContextItemsKey, out var ctxObj) &&
                ctxObj is SamlSpLogoutContext samlContext)
            {
                await HandleSamlIdpInitiatedLogoutAsync(samlContext);
            }
            else if (_context.Response.StatusCode == 200)
            {
                // Existing OIDC federated sign-out path (handler returned 200 with empty body)
                await _context.AuthenticateAsync();
                await ProcessFederatedSignOutRequestAsync();
            }
        }

        return result;
    }

    public Task<AuthenticateResult> AuthenticateAsync() => _inner.AuthenticateAsync();

    public Task ChallengeAsync(AuthenticationProperties properties) => _inner.ChallengeAsync(properties);

    public Task ForbidAsync(AuthenticationProperties properties) => _inner.ForbidAsync(properties);

    private async Task HandleSamlIdpInitiatedLogoutAsync(SamlSpLogoutContext samlContext)
    {
        _logger?.LogDebug("Processing SAML2 IdP-initiated federated signout");

        // The SAML handler has already written a response (303 redirect or POST form).
        // We need to intercept it. Since we set up the notification before the binding
        // writes the response, we can capture what was written. However, the response
        // may already have headers/status set. We need to check if we can still modify it.

        // Authenticate to populate session data (inbound cookie is still readable)
        await _context.AuthenticateAsync();

        // Store the SAML context for later response generation by the completion endpoint.
        var userSession = _context.RequestServices.GetRequiredService<IUserSession>();
        var user = await userSession.GetUserAsync(_context.RequestAborted);

        var logoutMessage = new SamlSpLogoutMessage
        {
            IdpEntityId = samlContext.IdpEntityId,
            LogoutRequestId = samlContext.LogoutRequestId,
            RelayState = samlContext.RelayState,
            ResponseBinding = samlContext.ResponseBinding,
            ResponseDestination = samlContext.ResponseDestination,
            SubjectId = user?.GetSubjectId(),
            SessionId = user != null ? await userSession.GetSessionIdAsync(_context.RequestAborted) : null
        };

        var messageStore = _context.RequestServices.GetRequiredService<IMessageStore<SamlSpLogoutMessage>>();
        var timeProvider = _context.RequestServices.GetRequiredService<TimeProvider>();
        var logoutId = await messageStore.WriteAsync(new Message<SamlSpLogoutMessage>(logoutMessage, timeProvider.GetUtcNow().UtcDateTime), _context.RequestAborted);

        // Check if downstream clients need notification.
        // Pass logoutId so the end-session-callback can track SAML SP responses.
        var iframeUrl = await _context.GetIdentityServerSignoutFrameCallbackUrlAsync(logoutId: logoutId);

        // Build the completion endpoint URL (needed for both paths below)
        var serverUrls = _context.RequestServices.GetRequiredService<IServerUrls>();
        var completionUrl = serverUrls.BaseUrl.EnsureTrailingSlash() + SamlConstants.Defaults.SpLogoutCompletionPath.TrimStart('/') + "?logoutId=" + Uri.EscapeDataString(logoutId);

        if (iframeUrl == null)
        {
            // No downstream clients — redirect to completion endpoint to send
            // the LogoutResponse back to the upstream IdP immediately.
            _logger?.LogDebug("No downstream clients to notify, redirecting to completion endpoint");
            _context.Response.Redirect(completionUrl);
            return;
        }

        _logger?.LogDebug("Stored SAML logout context with logoutId {LogoutId}, rendering combined page", logoutId);

        // Reset the response to render our combined HTML page.
        // Guard against the (unlikely) case where the SAML handler already started
        // streaming a response body — once started, headers are immutable.
        // This should not happen in practice because the SAML handler sets a redirect
        // status code and Location header without writing to the body.
        if (_context.Response.HasStarted)
        {
            _logger?.LogError("Cannot render combined logout page: response has already started. " +
                "The upstream IdP will not receive a LogoutResponse for this session");
            return;
        }

        // Validate the response destination before mutating the HTTP response.
        if (!Uri.TryCreate(samlContext.ResponseDestination, UriKind.Absolute, out var responseUri))
        {
            _logger?.LogError("Cannot render combined logout page: ResponseDestination is not a valid URI: {Destination}",
                samlContext.ResponseDestination);
            return;
        }

        _context.Response.StatusCode = 200;
        _context.Response.Headers.Remove("Location");
        _context.Response.ContentType = "text/html; charset=UTF-8";
        _context.Response.SetNoCache();

        // Allow this page to be framed only by the upstream IdP that initiated logout.
        var upstreamOrigin = responseUri.GetLeftPart(UriPartial.Authority);
        _context.Response.Headers["Content-Security-Policy"] = $"default-src 'none'; style-src {StyleHash}; script-src {ScriptHash}; frame-src 'self'; frame-ancestors {upstreamOrigin}";

        await RenderCombinedPageAsync(iframeUrl, completionUrl);
    }

    /// <summary>
    /// The inline script is completely static (no interpolated values) so it can be
    /// referenced by a SHA-256 hash in Content-Security-Policy. The completion URL is
    /// read from a data attribute on the iframe element.
    /// </summary>
    private static readonly string SignoutScript = GetEmbeddedResource($"{typeof(AuthenticationRequestHandlerWrapper).Namespace}.federated-signout.js");

    // Precomputed SHA-256 hash of the script for CSP script-src directive.
    private static readonly string ScriptHash = ComputeScriptHash();

    // SHA-256 hash of the inline <style> block: iframe{display:none;width:0;height:0;}
    private const string StyleHash = "'sha256-u+OupXgfekP+x/f6rMdoEAspPCYUtca912isERnoEjY='";

    private async Task RenderCombinedPageAsync(string iframeUrl, string completionUrl)
    {
        var htmlEncoder = HtmlEncoder.Default;

        var html = $$"""
            <!DOCTYPE html>
            <html>
            <head><title>Signing out</title><style>iframe{display:none;width:0;height:0;}</style></head>
            <body>
                <iframe id="signout-frame" width="0" height="0" src="{{htmlEncoder.Encode(iframeUrl)}}" data-completion-url="{{htmlEncoder.Encode(completionUrl)}}"></iframe>
                <noscript><p>Signing out&hellip; <a href="{{htmlEncoder.Encode(completionUrl)}}">Click here if not redirected automatically.</a></p></noscript>
                <script>{{SignoutScript}}</script>
            </body>
            </html>
            """;

        await _context.Response.WriteAsync(html);
        await _context.Response.Body.FlushAsync();
    }

    private static string ComputeScriptHash()
    {
        var bytes = Encoding.UTF8.GetBytes(SignoutScript);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return $"'sha256-{Convert.ToBase64String(hash)}'";
    }

    private static string GetEmbeddedResource(string resourceName)
    {
        var assembly = typeof(AuthenticationRequestHandlerWrapper).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task ProcessFederatedSignOutRequestAsync()
    {
        _logger?.LogDebug("Processing federated signout");

        var iframeUrl = await _context.GetIdentityServerSignoutFrameCallbackUrlAsync();
        if (iframeUrl != null)
        {
            _logger?.LogDebug("Rendering signout callback iframe");
            await RenderResponseAsync(iframeUrl);
        }
        else
        {
            _logger?.LogDebug("No signout callback iframe to render");
        }
    }

    private async Task RenderResponseAsync(string iframeUrl)
    {
        _context.Response.SetNoCache();

        if (_context.Response.Body.CanWrite)
        {
            var iframe = string.Format(CultureInfo.InvariantCulture, IframeHtml, iframeUrl);
            _context.Response.ContentType = "text/html";
            await _context.Response.WriteAsync(iframe);
            await _context.Response.Body.FlushAsync();
        }
    }
}
