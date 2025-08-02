// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Services;

public interface IUiLocalesService
{
    Task StoreUiLocalesForRedirectAsync(string? uiLocales);
}
