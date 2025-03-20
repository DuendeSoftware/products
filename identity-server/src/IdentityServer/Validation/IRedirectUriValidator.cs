// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models the logic when validating redirect and post logout redirect URIs.
/// </summary>
public interface IRedirectUriValidator
{
    /// <summary>
    /// Determines whether a redirect URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns><c>true</c> is the URI is valid; <c>false</c> otherwise.</returns>
    [Obsolete("This overload is deprecated and will be removed in a future version. Use the overload that takes a RedirectUriValidationContext parameter instead.")]
    Task<bool> IsRedirectUriValidAsync(string requestedUri, Client client);

    /// <summary>
    /// Determines whether a redirect URI is valid for a client.
    /// </summary>
    Task<bool> IsRedirectUriValidAsync(RedirectUriValidationContext context)
#pragma warning disable CS0618 // Type or member is obsolete
        => IsRedirectUriValidAsync(context.RequestedUri, context.Client);
#pragma warning restore CS0618 // Type or member is obsolete

    /// <summary>
    /// Determines whether a post logout URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The requested URI.</param>
    /// <param name="client">The client.</param>
    /// <returns><c>true</c> is the URI is valid; <c>false</c> otherwise.</returns>
    Task<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client);
}

/// <summary>
/// Models the context for validating a client's redirect URI
/// </summary>
public class RedirectUriValidationContext
{
    /// <summary>
    /// Default ctor
    /// </summary>
    public RedirectUriValidationContext()
    {
    }

    /// <summary>
    /// ctor
    /// </summary>
    public RedirectUriValidationContext(string redirectUri, ValidatedAuthorizeRequest request)
    {
        RequestedUri = redirectUri;
        Client = request.Client;
        RequestParameters = request.Raw;
        RequestObjectValues = request.RequestObjectValues;
        AuthorizeRequestType = request.AuthorizeRequestType;
    }

    /// <summary>
    /// The URI to validate for the client
    /// </summary>
    public string RequestedUri { get; set; } = null!;

    /// <summary>
    /// The client
    /// </summary>
    public Client Client { get; set; } = null!;

    /// <summary>
    /// The request parameters
    /// </summary>
    public NameValueCollection RequestParameters { get; set; } = null!;

    /// <summary>
    /// Validated request object values
    /// </summary>
    public IEnumerable<Claim>? RequestObjectValues { get; set; }

    /// <summary>
    /// Indicates the context (PAR vs Authorize with or without pushed parameters)
    /// </summary>
    public AuthorizeRequestType AuthorizeRequestType { get; set; }
}
