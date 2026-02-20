// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Services;

public interface IUiLocalesService
{
    /// <summary>
    /// Stores the UI locales for redirect.
    /// </summary>
    /// <param name="uiLocales"></param>
    /// <param name="ct"></param>
    Task StoreUiLocalesForRedirectAsync(string? uiLocales, CT ct);
}
