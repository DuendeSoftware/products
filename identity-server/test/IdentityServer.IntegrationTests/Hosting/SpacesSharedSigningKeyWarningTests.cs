// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.IntegrationTests.TestFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SpacesSharedSigningKeyWarningTests(WebServerFixture webServerFixture)
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(SpacesSigningKeyStore.DeveloperCredential)]
    [InlineData(SpacesSigningKeyStore.FileSystem)]
    public async Task Signing_keys_shared_across_spaces_log_a_warning_at_startup(SpacesSigningKeyStore store)
    {
        await using var host = await CreateHostAsync(store);

        WarningsAboutSharedKeys(host).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Signing_keys_stored_per_space_log_no_warning()
    {
        await using var host = await CreateHostAsync(SpacesSigningKeyStore.Storage);

        WarningsAboutSharedKeys(host).ShouldBeEmpty();
    }

    private Task<SpacesDataProtectionTestHost> CreateHostAsync(SpacesSigningKeyStore store) =>
        SpacesDataProtectionTestHost.CreateAsync(webServerFixture, nameof(SpacesSharedSigningKeyWarningTests),
            new SpacesDataProtectionTestHostOptions
            {
                SigningKeyStore = store,
                ConfigureServices = services => services.AddFakeLogging()
            }, _ct);

    private static List<FakeLogRecord> WarningsAboutSharedKeys(SpacesDataProtectionTestHost host) =>
        host.Services.GetRequiredService<FakeLogCollector>().GetSnapshot()
            .Where(r => r.Level == LogLevel.Warning && r.Id.Name == "SigningKeysSharedAcrossSpaces")
            .ToList();
}
