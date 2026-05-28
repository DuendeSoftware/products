// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validates redirect URIs and post-logout redirect URIs submitted in authorization and end-session requests.
/// </summary>
/// <remarks>
/// IdentityServer invokes this validator during the authorization request pipeline to confirm that
/// the <c>redirect_uri</c> parameter supplied by the client is permitted for that client, and during
/// the end-session pipeline to confirm that the <c>post_logout_redirect_uri</c> is permitted.
/// <para>
/// The default implementation performs an exact string match against the URIs registered on the
/// <see cref="Models.Client"/>. Override this interface to apply custom matching logic, such as
/// wildcard or pattern-based URI validation.
/// </para>
/// <para>
/// Register a custom implementation using <c>AddRedirectUriValidator&lt;T&gt;()</c> on the
/// IdentityServer builder.
/// </para>
/// </remarks>
public interface IRedirectUriValidator
{
    /// <summary>
    /// Determines whether a redirect URI is valid for a client.
    /// </summary>
    /// <param name="requestedUri">The <c>redirect_uri</c> value submitted in the authorization request.</param>
    /// <param name="client">The client whose registered redirect URIs should be checked.</param>
    /// <returns><c>true</c> if the URI is permitted for the client; <c>false</c> otherwise.</returns>
    [Obsolete("This overload is deprecated and will be removed in a future version. Use the overload that takes a RedirectUriValidationContext parameter instead.")]
    Task<bool> IsRedirectUriValidAsync(string requestedUri, Client client);

    /// <summary>
    /// Determines whether a redirect URI is valid for a client.
    /// </summary>
    /// <remarks>
    /// This overload is preferred over the deprecated string-based overload because it provides
    /// additional context such as the full request parameters, any validated request object values,
    /// and the type of authorize request (e.g., PAR vs. standard authorize).
    /// </remarks>
    /// <param name="context">
    /// The validation context containing the requested URI, the client, the raw request parameters,
    /// and the authorize request type.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns><c>true</c> if the URI is permitted for the client; <c>false</c> otherwise.</returns>
    Task<bool> IsRedirectUriValidAsync(RedirectUriValidationContext context, Ct ct)
#pragma warning disable CS0618 // Type or member is obsolete
        => IsRedirectUriValidAsync(context.RequestedUri, context.Client);
#pragma warning restore CS0618 // Type or member is obsolete

    /// <summary>
    /// Determines whether a post-logout redirect URI is valid for a client.
    /// </summary>
    /// <remarks>
    /// Called during end-session request processing to verify that the <c>post_logout_redirect_uri</c>
    /// parameter supplied by the client is registered and permitted.
    /// </remarks>
    /// <param name="requestedUri">The <c>post_logout_redirect_uri</c> value submitted in the end-session request.</param>
    /// <param name="client">The client whose registered post-logout redirect URIs should be checked.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns><c>true</c> if the URI is permitted for the client; <c>false</c> otherwise.</returns>
    Task<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client, Ct ct);
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
    public string RequestedUri { get; set; } = default!;

    /// <summary>
    /// The client
    /// </summary>
    public Client Client { get; set; } = default!;

    /// <summary>
    /// The request parameters
    /// </summary>
    public NameValueCollection RequestParameters { get; set; } = default!;

    /// <summary>
    /// Validated request object values
    /// </summary>
    public IEnumerable<Claim>? RequestObjectValues { get; set; }

    /// <summary>
    /// Indicates the context (PAR vs Authorize with or without pushed parameters)
    /// </summary>
    public AuthorizeRequestType AuthorizeRequestType { get; set; }
}
