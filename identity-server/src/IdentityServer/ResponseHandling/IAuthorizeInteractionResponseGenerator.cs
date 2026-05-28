// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Determines whether the user must log in, consent, create an account, or be redirected to a
/// custom page before the authorization endpoint can issue a response. This interface is invoked
/// during every authorization request, after the request has been validated but before the
/// authorization code or tokens are issued.
/// </summary>
/// <remarks>
/// The built-in implementation (<c>AuthorizeInteractionResponseGenerator</c>) encodes all default
/// login and consent semantics, including prompt handling, max-age enforcement, and ACR checks.
/// When customizing this behavior it is strongly recommended to derive from
/// <c>AuthorizeInteractionResponseGenerator</c> and override the relevant virtual methods rather
/// than implementing this interface from scratch, so that the default logic is preserved.
/// </remarks>
public interface IAuthorizeInteractionResponseGenerator
{
    /// <summary>
    /// Evaluates the current authorization request and returns an <see cref="InteractionResponse"/>
    /// that describes what interaction, if any, is required before the request can be completed.
    /// </summary>
    /// <param name="request">The validated authorize request being processed.</param>
    /// <param name="consent">
    /// The consent response provided by the user, if the user was shown a consent page and has
    /// already responded; otherwise <see langword="null"/>.
    /// </param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// An <see cref="InteractionResponse"/> indicating whether the user must log in, consent,
    /// create an account, be shown an error, be redirected to a custom page, or whether no
    /// further interaction is required.
    /// </returns>
    Task<InteractionResponse> ProcessInteractionAsync(ValidatedAuthorizeRequest request, ConsentResponse? consent, Ct ct);
}
