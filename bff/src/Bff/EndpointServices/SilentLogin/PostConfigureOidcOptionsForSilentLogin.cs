// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Bff;

/// <summary>
/// OIDC configuration to add silent login support
/// </summary>
public class PostConfigureOidcOptionsForSilentLogin : IPostConfigureOptions<OpenIdConnectOptions>
{
    private readonly string? _scheme;
    private readonly BffOpenIdConnectEvents _events;

    /// <summary>
    /// ctor
    /// </summary>
    public PostConfigureOidcOptionsForSilentLogin(
        IOptions<BffOptions> bffOptions,
        IOptions<AuthenticationOptions> authenticationOptions,
        ILoggerFactory logger)
    {
        _scheme = authenticationOptions.Value.DefaultChallengeScheme;
        _events = new BffOpenIdConnectEvents(logger.CreateLogger<BffOpenIdConnectEvents>(), bffOptions);
    }

    /// <inheritdoc />
    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        if (_scheme == name)
        {
            if (options.EventsType != null && !typeof(BffOpenIdConnectEvents).IsAssignableFrom(options.EventsType))
            {
                throw new Exception("EventsType on OpenIdConnectOptions must derive from BffOpenIdConnectEvents to work with the BFF framework.");
            }

            if (options.EventsType == null)
            {
                options.Events.OnRedirectToIdentityProvider = CreateRedirectCallback(options.Events.OnRedirectToIdentityProvider);
                options.Events.OnMessageReceived = CreateMessageReceivedCallback(options.Events.OnMessageReceived);
                options.Events.OnAuthenticationFailed = CreateAuthenticationFailedCallback(options.Events.OnAuthenticationFailed);
            }
        }
    }

    private Func<RedirectContext, Task> CreateRedirectCallback(Func<RedirectContext, Task> inner)
    {
        async Task Callback(RedirectContext ctx)
        {
            if (!await _events.ProcessRedirectToIdentityProviderAsync(ctx))
            {
                if (inner != null)
                {
                    await inner.Invoke(ctx);
                }
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
                if (inner != null)
                {
                    await inner.Invoke(ctx);
                }
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
                if (inner != null)
                {
                    await inner.Invoke(ctx);
                }
            }
        }

        return Callback;
    }
}
