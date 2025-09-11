// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Services;

internal class OidcReturnUrlParser : IReturnUrlParser
{
    private readonly IdentityServerOptions _options;
    private readonly IAuthorizeRequestValidator _validator;
    private readonly IUserSession _userSession;
    private readonly IServerUrls _urls;
    private readonly ILogger _logger;
    private readonly IAuthorizationParametersMessageStore _authorizationParametersMessageStore;

    public OidcReturnUrlParser(
        IdentityServerOptions options,
        IAuthorizeRequestValidator validator,
        IUserSession userSession,
        IServerUrls urls,
        ILogger<OidcReturnUrlParser> logger,
        IAuthorizationParametersMessageStore authorizationParametersMessageStore = null)
    {
        _options = options;
        _validator = validator;
        _userSession = userSession;
        _urls = urls;
        _logger = logger;
        _authorizationParametersMessageStore = authorizationParametersMessageStore;
    }

    public async Task<AuthorizationRequest> ParseAsync(string returnUrl)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("OidcReturnUrlParser.Parse");

        if (IsValidReturnUrl(returnUrl))
        {
            var parameters = returnUrl.ReadQueryStringAsNameValueCollection();
            if (_authorizationParametersMessageStore != null)
            {
                var messageStoreId = parameters[Constants.AuthorizationParamsStore.MessageStoreIdParameterName];
                var entry = await _authorizationParametersMessageStore.ReadAsync(messageStoreId);
                parameters = entry?.Data.FromFullDictionary() ?? [];
            }

            var user = await _userSession.GetUserAsync();
            var result = await _validator.ValidateAsync(parameters, user);
            if (!result.IsError)
            {
                _logger.LogTrace("AuthorizationRequest being returned");
                return new AuthorizationRequest(result.ValidatedRequest);
            }
        }

        _logger.LogTrace("No AuthorizationRequest being returned");
        return null;
    }

    public bool IsValidReturnUrl(string returnUrl)
    {
        using var activity = Tracing.ValidationActivitySource.StartActivity("OidcReturnUrlParser.IsValidReturnUrl");

        if (_options.UserInteraction.AllowOriginInReturnUrl && returnUrl.IsUri())
        {
            var host = _urls.Origin;
            if (returnUrl.StartsWith(host, StringComparison.OrdinalIgnoreCase) == true)
            {
                returnUrl = returnUrl.Substring(host.Length);
            }
        }

        if (returnUrl.IsLocalUrl())
        {
            {
                var index = returnUrl.IndexOf('?', StringComparison.InvariantCulture);
                if (index >= 0)
                {
                    returnUrl = returnUrl.Substring(0, index);
                }
            }
            {
                var index = returnUrl.IndexOf('#', StringComparison.InvariantCulture);
                if (index >= 0)
                {
                    returnUrl = returnUrl.Substring(0, index);
                }
            }

            if (returnUrl.EndsWith(ProtocolRoutePaths.Authorize, StringComparison.Ordinal) ||
                returnUrl.EndsWith(ProtocolRoutePaths.AuthorizeCallback, StringComparison.Ordinal))
            {
                _logger.LogTrace("returnUrl is valid");
                return true;
            }
        }

        _logger.LogTrace("returnUrl is not valid");
        return false;
    }
}
