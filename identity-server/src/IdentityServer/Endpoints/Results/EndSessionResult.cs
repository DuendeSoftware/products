// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Result for endsession
/// </summary>
/// <seealso cref="IEndpointResult" />
public class EndSessionResult : EndpointResult<EndSessionResult>
{
    /// <summary>
    /// The result
    /// </summary>
    public EndSessionValidationResult Result { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EndSessionResult"/> class.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <exception cref="System.ArgumentNullException">result</exception>
    public EndSessionResult(EndSessionValidationResult result) => Result = result ?? throw new ArgumentNullException(nameof(result));
}

internal class EndSessionHttpWriter : IHttpResponseWriter<EndSessionResult>
{
    public EndSessionHttpWriter(
        IdentityServerOptions options,
        TimeProvider timeProvider,
        IServerUrls urls,
        IMessageStore<LogoutMessage> logoutMessageStore,
        IUiLocalesService localesService)
    {
        _options = options;
        _timeProvider = timeProvider;
        _urls = urls;
        _logoutMessageStore = logoutMessageStore;
        _localesService = localesService;
    }

    private IdentityServerOptions _options;
    private TimeProvider _timeProvider;
    private IServerUrls _urls;
    private IMessageStore<LogoutMessage> _logoutMessageStore;
    private readonly IUiLocalesService _localesService;

    public async Task WriteHttpResponse(EndSessionResult result, HttpContext context)
    {
        var validatedRequest = result.Result.IsError ? null : result.Result.ValidatedRequest;

        string id = null;

        if (validatedRequest != null)
        {
            var logoutMessage = new LogoutMessage(validatedRequest);
            if (logoutMessage.ContainsPayload)
            {
                var msg = new Message<LogoutMessage>(logoutMessage, _timeProvider.GetUtcNow().UtcDateTime);
                id = await _logoutMessageStore.WriteAsync(msg);
            }
        }

        var redirect = _options.UserInteraction.LogoutUrl;

        if (redirect.IsLocalUrl())
        {
            redirect = _urls.GetIdentityServerRelativeUrl(redirect);
            await _localesService.StoreUiLocalesForRedirectAsync(result.Result.ValidatedRequest?.UiLocales);
        }

        if (id != null)
        {
            redirect = redirect.AddQueryString(_options.UserInteraction.LogoutIdParameter, id);
        }

        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = redirect;
    }
}
