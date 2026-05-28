// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Services;

public interface IUiLocalesService
{
    /// <summary>
    /// Stores the UI locales for redirect.
    /// </summary>
    /// <param name="uiLocales">A space-delimited string of BCP 47 language tags representing the end-user's preferred languages, as specified by the OpenID Connect <c>ui_locales</c> parameter. May be <c>null</c> if no preference was expressed.</param>
    /// <param name="ct">The cancellation token.</param>
    Task StoreUiLocalesForRedirectAsync(string? uiLocales, Ct ct);
}
