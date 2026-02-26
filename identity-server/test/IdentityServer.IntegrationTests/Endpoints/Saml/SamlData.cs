// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Time.Testing;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

internal class SamlData
{
    public DateTimeOffset Now => FakeTimeProvider.GetUtcNow();

    public FakeTimeProvider FakeTimeProvider =
        new FakeTimeProvider(new DateTimeOffset(2000, 1, 2, 3, 4, 5, TimeSpan.Zero));

    public string EntityId = "https://sp.example.com";

    public Uri AcsUrl = new Uri("https://sp.example.com/callback");

    public Uri SingleLogoutServiceUrl = new Uri("https://sp.example.com/logout");

    public string RequestId = "_request-123";

    public string? RelayState = "some_state";
}
