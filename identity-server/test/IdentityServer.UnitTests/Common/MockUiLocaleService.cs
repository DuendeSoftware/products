// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class MockUiLocaleService : IUiLocalesService
{
    public Task StoreUiLocalesForRedirectAsync(string? uiLocales, Ct ct) => Task.CompletedTask;
}
