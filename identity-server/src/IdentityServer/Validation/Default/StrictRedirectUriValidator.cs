// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default implementation of redirect URI validator. Validates the URIs against
/// the client's configured URIs.
/// </summary>
public class StrictRedirectUriValidator : IRedirectUriValidator
{
    private readonly IdentityServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="StrictRedirectUriValidator" />.
    /// </summary>
    /// <param name="options"></param>
    public StrictRedirectUriValidator(IdentityServerOptions options = null) => _options = options;

    /// <summary>
    /// Checks if a given URI string is in a collection of strings (using ordinal ignore case comparison)
    /// </summary>
    /// <param name="uris">The uris.</param>
    /// <param name="requestedUri">The requested URI.</param>
    /// <returns></returns>
    protected bool StringCollectionContainsString(IEnumerable<string> uris, string requestedUri)
    {
        if (IEnumerableExtensions.IsNullOrEmpty(uris))
        {
            return false;
        }

        return uris.Contains(requestedUri, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether a redirect URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns>
    ///   <c>true</c> is the URI is valid; <c>false</c> otherwise.
    /// </returns>
    public virtual Task<bool> IsRedirectUriValidAsync(string requestedUri, Client client) => Task.FromResult(StringCollectionContainsString(client.RedirectUris, requestedUri));

    /// <summary>
    /// Determines whether a post logout URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns>
    ///   <c>true</c> is the URI is valid; <c>false</c> otherwise.
    /// </returns>
    public virtual Task<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client) => Task.FromResult(StringCollectionContainsString(client.PostLogoutRedirectUris, requestedUri));

    /// <summary>
    /// Determines whether a redirect uri is valid for a context.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>
    ///   <c>true</c> is the URI is valid; <c>false</c> otherwise.
    /// </returns>
    public virtual Task<bool> IsRedirectUriValidAsync(RedirectUriValidationContext context)
    {
        // Check if special case handling for PAR is enabled and that the client
        // is a confidential client. If so, any pushed redirect uri is allowed
        // on the PAR endpoint and at the authorize endpoint (if a redirect uri
        // was pushed)
        if (_options?.PushedAuthorization?.AllowUnregisteredPushedRedirectUris == true &&
            context.Client.RequireClientSecret &&
            (context.AuthorizeRequestType == AuthorizeRequestType.PushedAuthorization ||
             context.AuthorizeRequestType == AuthorizeRequestType.AuthorizeWithPushedParameters))
        {
            return Task.FromResult(true);
        }
        // Otherwise, just use the default strict validation
        return IsRedirectUriValidAsync(context.RequestedUri, context.Client);
    }
}
