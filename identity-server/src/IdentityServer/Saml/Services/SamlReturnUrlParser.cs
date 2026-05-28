// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Services;

internal sealed class SamlReturnUrlParser(
    ISamlSigninStateStore stateStore,
    ISamlServiceProviderStore serviceProviderStore,
    IOptions<IdentityServerOptions> identityServerOptions,
    IServerUrls serverUrls,
    ILogger<SamlReturnUrlParser> logger) : IReturnUrlParser
{
    public bool IsValidReturnUrl(string returnUrl)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("SamlReturnUrlParser.IsValidReturnUrl");

        if (identityServerOptions.Value.UserInteraction.AllowOriginInReturnUrl && returnUrl.IsUri())
        {
            var host = serverUrls.Origin;
            if (returnUrl.StartsWith(host, StringComparison.OrdinalIgnoreCase))
            {
                returnUrl = returnUrl[host.Length..];
            }
        }

        if (!returnUrl.IsLocalUrl())
        {
            logger.ReturnUrlIsNotLocalUrl(LogLevel.Trace);
            return false;
        }

        var callbackPath = identityServerOptions.Value.Saml.Endpoints.SingleSignOnCallbackPath;
        var stateIdParam = identityServerOptions.Value.Saml.Endpoints.StateIdParameterName;

        // Strip query string to compare path
        var path = returnUrl;
        var queryIndex = returnUrl.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            path = returnUrl[..queryIndex];
        }

        if (!path.EndsWith(callbackPath, StringComparison.OrdinalIgnoreCase))
        {
            logger.ReturnUrlDoesNotMatchSamlCallbackPath(LogLevel.Trace);
            return false;
        }

        // Must have a samlStateId query parameter
        if (queryIndex < 0)
        {
            logger.ReturnUrlHasNoQueryString(LogLevel.Trace);
            return false;
        }

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(returnUrl[queryIndex..]);
        if (!query.ContainsKey(stateIdParam))
        {
            logger.ReturnUrlMissingStateIdParam(LogLevel.Trace, stateIdParam);
            return false;
        }

        logger.ReturnUrlIsValidSamlCallbackUrl(LogLevel.Trace);
        return true;
    }

    public async Task<IAuthenticationContext?> ParseAsync(string returnUrl, Ct ct)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("SamlReturnUrlParser.Parse");

        if (!IsValidReturnUrl(returnUrl))
        {
            logger.NoSamlAuthenticationContextBeingReturned(LogLevel.Trace);
            return null;
        }

        var stateIdParam = identityServerOptions.Value.Saml.Endpoints.StateIdParameterName;
        var queryIndex = returnUrl.IndexOf('?', StringComparison.Ordinal);
        var queryString = returnUrl[queryIndex..];

        // Strip any fragment before parsing to avoid polluting parameter values
        var fragmentIndex = queryString.IndexOf('#', StringComparison.Ordinal);
        if (fragmentIndex >= 0)
        {
            queryString = queryString[..fragmentIndex];
        }

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryString);

        if (!query.TryGetValue(stateIdParam, out var stateIdValues) ||
            !Guid.TryParse(stateIdValues.FirstOrDefault(), out var stateIdGuid))
        {
            logger.CouldNotParseStateIdParamAsGuid(LogLevel.Trace, stateIdParam);
            return null;
        }

        var state = await stateStore.RetrieveSigninRequestStateAsync(stateIdGuid, ct);
        if (state is null)
        {
            logger.NoSamlStateFoundForStateId(LogLevel.Trace, stateIdGuid);
            return null;
        }

        var sp = await serviceProviderStore.FindByEntityIdAsync(state.ServiceProviderEntityId, ct);
        if (sp is null)
        {
            logger.NoServiceProviderFoundForEntityId(LogLevel.Trace, state.ServiceProviderEntityId);
            return null;
        }

        logger.SamlAuthenticationContextBeingReturned(LogLevel.Trace);
        return new SamlAuthenticationContext(state, sp, stateIdGuid);
    }
}
