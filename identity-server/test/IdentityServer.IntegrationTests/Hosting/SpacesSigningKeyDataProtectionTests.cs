// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.Spaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

// Automatically managed signing keys are data protected through the unpartitioned purpose chain, so a
// key written by a host with space binding off is readable by a host with it on (and the other way round)
// in every space. Two hosts share one key ring, one database and one signing key directory; the first host
// is disposed before the second starts.
public sealed class SpacesSigningKeyDataProtectionTests(WebServerFixture webServerFixture)
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public static TheoryData<SpacesSigningKeyStore, bool> StoresAndDirections
    {
        get
        {
            var data = new TheoryData<SpacesSigningKeyStore, bool>();
            foreach (var store in new[] { SpacesSigningKeyStore.FileSystem, SpacesSigningKeyStore.Storage })
            {
                data.Add(store, true);
                data.Add(store, false);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(StoresAndDirections))]
    public async Task keys_created_by_one_host_are_served_unchanged_by_a_host_with_the_other_partitioning_setting(
        SpacesSigningKeyStore store, bool firstHostPartitions)
    {
        using var keyRing = new SpacesTestKeyRing();
        using var storage = new SpacesTestSharedStorage();

        var before = await CreateKeysAsync(store, keyRing, storage, partition: firstHostPartitions);

        await using var env = await CreateHostAsync(store, keyRing, storage, partition: !firstHostPartitions);
        foreach (var space in new[] { env.DefaultSpace, env.SpaceA })
        {
            var expected = before[space.Id];
            var served = await GetJwksAsync(env, space);

            served.Keys.Select(k => k.Kid).Order().ToArray().ShouldBe(expected.JwksKeyIds,
                "the second host must serve the keys the first host created");
            served.Keys.Select(k => k.Kid).ShouldContain(expected.Token.Kid, "the token signing key must still be served");

            var stored = await LoadStoredKeyIdsAsync(env, space);
            stored.ShouldBe(expected.StoredKeyIds, "the second host must not create a new key");

            var validation = await new JsonWebTokenHandler().ValidateTokenAsync(expected.Token.Jwt, new TokenValidationParameters
            {
                IssuerSigningKeys = served.Keys,
                ValidateIssuer = false,
                ValidateAudience = false
            });
            validation.IsValid.ShouldBeTrue("a token issued by the first host must validate against the second host's keys");
        }
    }

    private async Task<Dictionary<SpaceId, KeySnapshot>> CreateKeysAsync(
        SpacesSigningKeyStore store, SpacesTestKeyRing keyRing, SpacesTestSharedStorage storage, bool partition)
    {
        await using var env = await CreateHostAsync(store, keyRing, storage, partition);
        var result = new Dictionary<SpaceId, KeySnapshot>();
        foreach (var space in new[] { env.DefaultSpace, env.SpaceA })
        {
            var jwt = await env.RunInSpaceAsync(space.Id, async services =>
                await services.GetRequiredService<ITokenCreationService>().CreateTokenAsync(new Token(OidcConstants.TokenTypes.AccessToken)
                {
                    Issuer = space.Issuer,
                    CreationTime = DateTime.UtcNow,
                    Lifetime = 600,
                    ClientId = "client",
                    AccessTokenType = AccessTokenType.Jwt,
                    Audiences = { "api" },
                    Claims = { new Claim(JwtClaimTypes.Subject, "alice") }
                }, _ct));
            var kid = new JsonWebToken(jwt).Kid;

            var stored = await LoadStoredKeysAsync(env, space);
            stored.ShouldNotBeEmpty("the host must have created a managed signing key");
            stored.ShouldAllBe(k => k.DataProtected, "managed signing keys must be data protected");

            var jwks = await GetJwksAsync(env, space);
            var kids = jwks.Keys.Select(k => k.Kid).Order().ToArray();
            kids.ShouldContain(kid);
            result[space.Id] = new KeySnapshot(new IssuedToken(jwt, kid), kids, stored.Select(k => k.Id).Order().ToArray());
        }

        return result;
    }

    private Task<SpacesDataProtectionTestHost> CreateHostAsync(
        SpacesSigningKeyStore store, SpacesTestKeyRing keyRing, SpacesTestSharedStorage storage, bool partition) =>
        SpacesDataProtectionTestHost.CreateAsync(webServerFixture, nameof(SpacesSigningKeyDataProtectionTests), new SpacesDataProtectionTestHostOptions
        {
            KeyRing = keyRing,
            SharedStorage = storage,
            PartitionDataProtection = partition,
            FallbackToDefault = true,
            SigningKeyStore = store
        }, _ct);

    private static Task<IReadOnlyCollection<SerializedKey>> LoadStoredKeysAsync(SpacesDataProtectionTestHost host, SpacesTestSpace space) =>
        host.RunInSpaceAsync<IReadOnlyCollection<SerializedKey>>(space.Id, services =>
            services.GetRequiredService<ISigningKeyStore>().LoadKeysAsync(TestContext.Current.CancellationToken));

    private static async Task<string[]> LoadStoredKeyIdsAsync(SpacesDataProtectionTestHost host, SpacesTestSpace space) =>
        [.. (await LoadStoredKeysAsync(host, space)).Select(k => k.Id).Order()];

    private async Task<JsonWebKeySet> GetJwksAsync(SpacesDataProtectionTestHost env, SpacesTestSpace space)
    {
        var json = await env.GetStringAsync(space, "/.well-known/openid-configuration/jwks", _ct);
        return new JsonWebKeySet(json);
    }

    private sealed record IssuedToken(string Jwt, string Kid);

    private sealed record KeySnapshot(IssuedToken Token, string[] JwksKeyIds, string[] StoredKeyIds);
}
