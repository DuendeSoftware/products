// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;

namespace Duende.Bff;

/// <summary>
/// BFF specific OpenIdConnectEvents class.
/// </summary>
public class BffOpenIdConnectEvents : OpenIdConnectEvents
{
    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger<BffOpenIdConnectEvents> Logger;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="logger"></param>
    public BffOpenIdConnectEvents(ILogger<BffOpenIdConnectEvents> logger)
    {
        Logger = logger;
    }

    /// <inheritdoc/>
    public override async Task RedirectToIdentityProvider(RedirectContext context)
    {
        if (!await ProcessRedirectToIdentityProviderAsync(context))
        {
            await base.RedirectToIdentityProvider(context);
        }
    }

    /// <summary>
    /// Processes the RedirectToIdentityProvider event.
    /// </summary>
    public virtual Task<bool> ProcessRedirectToIdentityProviderAsync(RedirectContext context)
    {
        if (context.Properties?.IsSilentLogin() == true)
        {
            Logger.LogDebug("Setting OIDC ProtocolMessage.Prompt to 'none' for BFF silent login");
            context.ProtocolMessage.Prompt = "none";
        }

        // we've not "handled" the request, so let other code process
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public override async Task MessageReceived(MessageReceivedContext context)
    {
        if (!await ProcessMessageReceivedAsync(context))
        {
            await base.MessageReceived(context);
        }
    }

    /// <summary>
    /// Processes the MessageReceived event.
    /// </summary>
    public virtual Task<bool> ProcessMessageReceivedAsync(MessageReceivedContext context)
    {
        if (context.Properties?.IsSilentLogin() == true &&
            context.Properties?.RedirectUri != null)
        {
            context.HttpContext.Items[Constants.BffFlags.SilentLogin] = context.Properties.RedirectUri;

            if (context.ProtocolMessage.Error != null)
            {
                Logger.LogDebug("Handling error response from OIDC provider for BFF silent login.");

                context.HandleResponse();
                context.Response.Redirect(context.Properties.RedirectUri);
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public override async Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        if (!await ProcessAuthenticationFailedAsync(context))
        {
            await base.AuthenticationFailed(context);
        }
    }

    /// <summary>
    /// Processes the AuthenticationFailed event.
    /// </summary>
    public virtual Task<bool> ProcessAuthenticationFailedAsync(AuthenticationFailedContext context)
    {
        if (context.HttpContext.Items.ContainsKey(Constants.BffFlags.SilentLogin))
        {
            Logger.LogDebug("Handling failed response from OIDC provider for BFF silent login.");

            context.HandleResponse();
            context.Response.Redirect(context.HttpContext.Items[Constants.BffFlags.SilentLogin]!.ToString()!);

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
