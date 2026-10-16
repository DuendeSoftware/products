// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using System.Web;
using Duende.IdentityServer.IntegrationTests.TestFramework;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

// Host 1 runs without space-partitioned data protection (or without Spaces at all) and issues a session
// cookie with the ticket in the cookie. Host 2 shares the key ring and the storage with Host 1 and runs
// with partitioning on, as an upgraded deployment does. The cookie is captured at a local sign-in on Host 1
// and sent to Host 2 in a raw Cookie header.
public sealed class SpacesDataProtectionMigrationTests(WebServerFixture webServerFixture)
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task default_space_cookie_issued_before_partitioning_is_accepted_in_default_after_partitioning_is_enabled(bool host1HasSpaces)
    {
        using var keyRing = new SpacesTestKeyRing();
        using var storage = new SpacesTestSharedStorage();
        var cookies = await SignInOnHost1Async(keyRing, storage, host1HasSpaces);
        await using var host2 = await CreateHost2Async(keyRing, storage);

        using var response = await host2.AuthorizeAsync(host2.DefaultSpace, cookies, _ct);

        AssertIssuedCode(response);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task default_space_cookie_issued_before_partitioning_is_rejected_in_a_named_space_after_partitioning_is_enabled(bool host1HasSpaces)
    {
        using var keyRing = new SpacesTestKeyRing();
        using var storage = new SpacesTestSharedStorage();
        var cookies = await SignInOnHost1Async(keyRing, storage, host1HasSpaces);
        await using var host2 = await CreateHost2Async(keyRing, storage);

        using var response = await host2.AuthorizeAsync(host2.SpaceA, cookies, _ct);

        AssertRedirectsToLogin(response);
    }

    // Host 1 is disposed before Host 2 starts. Its data stays in the shared storage.
    private async Task<Dictionary<string, string>> SignInOnHost1Async(
        SpacesTestKeyRing keyRing, SpacesTestSharedStorage storage, bool host1HasSpaces)
    {
        await using var host1 = await SpacesDataProtectionTestHost.CreateAsync(webServerFixture, nameof(SpacesDataProtectionMigrationTests),
            new SpacesDataProtectionTestHostOptions
            {
                KeyRing = keyRing,
                SharedStorage = storage,
                EnableSpaces = host1HasSpaces,
                PartitionDataProtection = false,
                FallbackToDefault = true
            }, _ct);

        return await host1.SignInAsync(host1.DefaultSpace, _ct);
    }

    private Task<SpacesDataProtectionTestHost> CreateHost2Async(SpacesTestKeyRing keyRing, SpacesTestSharedStorage storage) =>
        SpacesDataProtectionTestHost.CreateAsync(webServerFixture, nameof(SpacesDataProtectionMigrationTests),
            new SpacesDataProtectionTestHostOptions
            {
                KeyRing = keyRing,
                SharedStorage = storage,
                PartitionDataProtection = true,
                FallbackToDefault = true
            }, _ct);

    private static void AssertIssuedCode(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        var location = response.Headers.Location!;
        location.ToString().ShouldStartWith("https://rp.example/callback", customMessage: "the session cookie must be accepted");
        HttpUtility.ParseQueryString(location.Query)["code"].ShouldNotBeNullOrEmpty();
    }

    private static void AssertRedirectsToLogin(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        var location = response.Headers.Location!.ToString();
        location.ShouldNotStartWith("https://rp.example/callback", customMessage: "the session cookie must be rejected");
        location.ShouldContain("/account/login");
    }
}
