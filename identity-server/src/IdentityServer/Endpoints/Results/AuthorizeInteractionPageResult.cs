// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Endpoints.Results;

/// <summary>
/// Result for an interactive page
/// </summary>
/// <seealso cref="IEndpointResult" />
public abstract class AuthorizeInteractionPageResult : EndpointResult<AuthorizeInteractionPageResult>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizeInteractionPageResult"/> class.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="redirectUrl"></param>
    /// <param name="returnUrlParameterName"></param>
    /// <exception cref="System.ArgumentNullException">request</exception>
    protected AuthorizeInteractionPageResult(ValidatedAuthorizeRequest request, string redirectUrl, string returnUrlParameterName)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        RedirectUrl = redirectUrl ?? throw new ArgumentNullException(nameof(redirectUrl));
        ReturnUrlParameterName = returnUrlParameterName ?? throw new ArgumentNullException(nameof(returnUrlParameterName));
    }

    /// <summary>
    /// The validated authorize request
    /// </summary>
    public ValidatedAuthorizeRequest Request { get; }

    /// <summary>
    /// The redirect URI
    /// </summary>
    public string RedirectUrl { get; }

    /// <summary>
    /// The return URL param name
    /// </summary>
    public string ReturnUrlParameterName { get; }
}
