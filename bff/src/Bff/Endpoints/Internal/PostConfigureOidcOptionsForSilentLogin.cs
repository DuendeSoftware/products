// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints.Internal;

/// <summary>
/// OIDC configuration to add silent login support
/// </summary>
internal class PostConfigureOidcOptionsForSilentLogin(
    SelectedFrontend selectedFrontend,
    IOptions<BffOptions> bffOptions,
    IOptions<AuthenticationOptions> options, ILoggerFactory logger) : IPostConfigureOptions<OpenIdConnectOptions>
{
    private readonly string? _defaultChallengeScheme = options.Value.DefaultChallengeScheme;
    private readonly BffOpenIdConnectEvents _events = new(bffOptions, logger.CreateLogger<BffOpenIdConnectEvents>());

    /// <inheritdoc />
    public void PostConfigure(string? scheme, OpenIdConnectOptions options)
    {
        if (_defaultChallengeScheme != scheme && (!selectedFrontend.TryGet(out var frontend) || scheme != frontend.OidcSchemeName))
        {
            return;
        }

        if (options.EventsType != null && !typeof(BffOpenIdConnectEvents).IsAssignableFrom(options.EventsType))
        {
            throw new InvalidOperationException("EventsType on OpenIdConnectOptions must derive from BffOpenIdConnectEvents to work with the BFF framework.");
        }

        if (options.EventsType != null)
        {
            return;
        }

        options.Events.OnRedirectToIdentityProvider = CreateRedirectCallback(options.Events.OnRedirectToIdentityProvider);
        options.Events.OnMessageReceived = CreateMessageReceivedCallback(options.Events.OnMessageReceived);
        options.Events.OnAuthenticationFailed = CreateAuthenticationFailedCallback(options.Events.OnAuthenticationFailed);
    }

    private Func<RedirectContext, Task> CreateRedirectCallback(Func<RedirectContext, Task> inner)
    {
        async Task Callback(RedirectContext ctx)
        {
            if (!await _events.ProcessRedirectToIdentityProviderAsync(ctx))
            {
                await inner.Invoke(ctx);
            }
        }

        return Callback;
    }

    private Func<MessageReceivedContext, Task> CreateMessageReceivedCallback(Func<MessageReceivedContext, Task> inner)
    {
        async Task Callback(MessageReceivedContext ctx)
        {
            if (!await _events.ProcessMessageReceivedAsync(ctx))
            {
                await inner.Invoke(ctx);
            }
        }

        return Callback;
    }

    private Func<AuthenticationFailedContext, Task> CreateAuthenticationFailedCallback(Func<AuthenticationFailedContext, Task> inner)
    {
        async Task Callback(AuthenticationFailedContext ctx)
        {
            if (!await _events.ProcessAuthenticationFailedAsync(ctx))
            {
                await inner.Invoke(ctx);
            }
        }

        return Callback;
    }
}
