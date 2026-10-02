// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Net;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

public sealed class SpacesFederationTests(WebServerFixture webServerFixture) : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // Starts both spaces' per-iteration tasks for one step before awaiting any of them, so
    // the two spaces' requests genuinely race instead of running as two sequential batches.
    private static async Task<(T[] SpaceAResults, T[] SpaceBResults)> WhenAllBothSpacesAsync<T>(
        Func<int, Task<T>> spaceATaskFactory, Func<int, Task<T>> spaceBTaskFactory, int iterations)
    {
        var spaceATasks = Enumerable.Range(0, iterations).Select(spaceATaskFactory).ToArray();
        var spaceBTasks = Enumerable.Range(0, iterations).Select(spaceBTaskFactory).ToArray();

        await Task.WhenAll(spaceATasks.Concat(spaceBTasks));

        return (spaceATasks.Select(t => t.Result).ToArray(), spaceBTasks.Select(t => t.Result).ToArray());
    }

    // Asserts that after deleting a provider, a subsequent challenge either fails outright
    // (status >= 400) or redirects somewhere other than either IdP's host. A redirect to the
    // OTHER space's IdP would be the exact cross-space leak this guards against, so it is
    // checked explicitly rather than only checking against the deleted provider's own IdP.
    private static async Task AssertChallengeIsUnreachableAfterDeleteAsync(HttpClient mainClient, string challengeUrl, KestrelBasedTestServer deletedProviderIdp, KestrelBasedTestServer otherSpaceIdp)
    {
        var challengeAfterDelete = await mainClient.GetAsync(challengeUrl);
        var status = (int)challengeAfterDelete.StatusCode;
        if (status is >= 300 and < 400)
        {
            var location = new Uri(mainClient.BaseAddress!, challengeAfterDelete.Headers.Location!);
            location.Host.ShouldNotBe(deletedProviderIdp.BaseAddress.Host,
                "a deleted provider must never be reached at its own IdP even if the challenge somehow redirects");
            location.Host.ShouldNotBe(otherSpaceIdp.BaseAddress.Host,
                "a deleted provider must never fall through to the other space's IdP");
        }
        else
        {
            status.ShouldBeGreaterThanOrEqualTo(400,
                $"observed status after deleting the provider: {challengeAfterDelete.StatusCode}");
        }
    }

    [Fact]
    public async Task signing_in_to_space_one_then_space_two_each_lands_at_its_own_idp()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(signing_in_to_space_one_then_space_two_each_lands_at_its_own_idp), _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);
        await host.CreateOidcProviderForLoginAsync(host.SpaceBId, idp2Authority, "client", _ct);

        var resultA = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        resultA.ChallengeRedirectHost.ShouldBe(host.Idp1.BaseAddress.Host,
            "space A's challenge must redirect straight to IdP 1's authorize endpoint, never IdP 2's");
        resultA.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        resultA.Issuer.ShouldBe(idp1Authority);

        var resultB = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        resultB.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host,
            "space B's challenge must redirect straight to IdP 2's authorize endpoint, never IdP 1's");
        resultB.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        resultB.Issuer.ShouldBe(idp2Authority);

        // The two subjects and issuers are distinct: a space-2 challenge never reaches IdP 1,
        // and neither IdP's identity can be confused with the other's.
        resultA.Subject.ShouldNotBe(resultB.Subject);
        resultA.Issuer.ShouldNotBe(resultB.Issuer);
    }

    [Fact]
    public async Task signing_out_of_space_one_sends_the_end_session_request_to_its_own_idp()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(signing_out_of_space_one_sends_the_end_session_request_to_its_own_idp), _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);
        await host.CreateOidcProviderForLoginAsync(host.SpaceBId, idp2Authority, "client", _ct);

        var endSessionRedirect = await SpacesFederationTestHost.RunOidcSignInThenLogoutAsync(
            host.FederationServer, host.Idp1, "/t/space-a");

        endSessionRedirect.Host.ShouldBe(host.Idp1.BaseAddress.Host,
            "space A's sign-out must send the end-session request to IdP 1, never IdP 2");
        endSessionRedirect.Host.ShouldNotBe(host.Idp2.BaseAddress.Host);
        endSessionRedirect.AbsolutePath.ShouldBe("/connect/endsession");
    }

    [Fact]
    public async Task signing_out_of_space_two_sends_the_end_session_request_to_its_own_idp()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(signing_out_of_space_two_sends_the_end_session_request_to_its_own_idp), _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);
        await host.CreateOidcProviderForLoginAsync(host.SpaceBId, idp2Authority, "client", _ct);

        var endSessionRedirect = await SpacesFederationTestHost.RunOidcSignInThenLogoutAsync(
            host.FederationServer, host.Idp2, "/t/space-b");

        endSessionRedirect.Host.ShouldBe(host.Idp2.BaseAddress.Host,
            "space B's sign-out must send the end-session request to IdP 2, never IdP 1");
        endSessionRedirect.Host.ShouldNotBe(host.Idp1.BaseAddress.Host);
        endSessionRedirect.AbsolutePath.ShouldBe("/connect/endsession");
    }

    [Fact]
    public async Task a_browser_does_not_send_a_space_one_session_to_space_two()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(a_browser_does_not_send_a_space_one_session_to_space_two), _ct);

        var idp1Authority = host.Idp1Authority;
        await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);

        using var mainClient = SpacesFederationTestHost.CreateBrowserClient(host.FederationServer);
        var signInResult = await SpacesFederationTestHost.RunOidcSignInAsync(mainClient, host.Idp1, "/t/space-a");
        signInResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);

        // The browser's cookie path scoping keeps the space A session cookie out of space B
        // requests, so space B's authorize endpoint sees no session and redirects to login.
        // The server does not bind sessions to a space; a replayed space A cookie is accepted
        // by space B (#3685).
        var authorize = await mainClient.GetAsync(
            $"/t/space-b/connect/authorize?client_id={SpacesFederationTestHost.SpaceBClientId}&response_type=code&scope=openid" +
            "&redirect_uri=https%3A%2F%2Frp.example%2Fcallback&code_challenge=abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ" +
            "&code_challenge_method=S256&state=s1");

        authorize.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        authorize.Headers.Location.ShouldNotBeNull();
        authorize.Headers.Location.ToString().ShouldNotStartWith("https://rp.example/callback");
        authorize.Headers.Location.ToString().ShouldContain("/account/login");
    }

    [Fact]
    public async Task changing_a_provider_at_runtime_makes_the_next_sign_in_use_the_new_config()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(changing_a_provider_at_runtime_makes_the_next_sign_in_use_the_new_config), _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        var providerId = await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);

        var firstResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        firstResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        firstResult.Issuer.ShouldBe(idp1Authority);

        await host.UpdateOidcProviderAuthorityAsync(host.SpaceAId, providerId, idp2Authority, _ct);

        var secondResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-a");
        secondResult.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host,
            "after repointing space 1's provider at IdP 2, the next sign-in's challenge must redirect to IdP 2");
        secondResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        secondResult.Issuer.ShouldBe(idp2Authority);
    }

    [Fact]
    public async Task adding_a_provider_at_runtime_within_a_space_is_usable_immediately()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(adding_a_provider_at_runtime_within_a_space_is_usable_immediately), _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        // Space 2's provider is created and used first.
        await host.CreateOidcProviderForLoginAsync(host.SpaceBId, idp2Authority, "client", _ct);
        var spaceBResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBResult.Issuer.ShouldBe(idp2Authority);

        // Space 1's provider is created for the first time, only now. It must be usable
        // immediately, with no restart or warmup.
        await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);
        var spaceAResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        spaceAResult.ChallengeRedirectHost.ShouldBe(host.Idp1.BaseAddress.Host);
        spaceAResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        spaceAResult.Issuer.ShouldBe(idp1Authority);
    }

    [Fact]
    public async Task removing_a_provider_makes_it_unusable_then_re_adding_with_a_new_config_uses_the_new_config()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture,
            nameof(removing_a_provider_makes_it_unusable_then_re_adding_with_a_new_config_uses_the_new_config),
            _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        var providerId = await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);
        await host.CreateOidcProviderForLoginAsync(host.SpaceBId, idp2Authority, "client", _ct);

        var firstResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        firstResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);

        await host.DeleteOidcProviderAsync(host.SpaceAId, providerId, _ct);

        // Confirm space 1 is unusable: whatever the challenge produces, it must not be a
        // redirect to either IdP. Empirically, the current behavior is an HTTP 500 from the
        // challenge (the scheme can no longer be resolved to a configured handler); this is
        // asserted precisely without pinning 500 as the contractually required status.
        using (var mainClient = SpacesFederationTestHost.CreateBrowserClient(host.FederationServer))
        {
            await AssertChallengeIsUnreachableAfterDeleteAsync(mainClient, "/t/space-a/test/challenge", host.Idp1, host.Idp2);
        }

        // Space 2 is untouched by space 1's deletion.
        var spaceBStillWorks = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBStillWorks.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBStillWorks.Issuer.ShouldBe(idp2Authority);

        // Re-add space 1's provider with a DIFFERENT config (pointing at IdP 2), proving no
        // stale cached/tracked entry survives the delete.
        await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp2Authority, "client", _ct);
        var reAddedResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-a");
        reAddedResult.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host);
        reAddedResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        reAddedResult.Issuer.ShouldBe(idp2Authority);

        // Space 2 remains unaffected throughout.
        var spaceBFinalCheck = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBFinalCheck.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBFinalCheck.Issuer.ShouldBe(idp2Authority);
    }

    [Fact]
    public async Task host_resolved_spaces_each_sign_in_to_their_own_idp()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(host_resolved_spaces_each_sign_in_to_their_own_idp), _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        var (hostAOrigin, hostASpaceId, hostBOrigin, hostBSpaceId) = await host.CreateHostResolvedSpacesAsync(_ct);

        await host.CreateOidcProviderForLoginAsync(hostASpaceId, idp1Authority, "client", _ct);
        await host.CreateOidcProviderForLoginAsync(hostBSpaceId, idp2Authority, "client", _ct);

        using var hostAClient = SpacesFederationTestHost.CreateBrowserClientForOrigin(host.FederationServer, hostAOrigin);
        var resultA = await SpacesFederationTestHost.RunOidcSignInAsync(hostAClient, host.Idp1, urlPrefix: "");
        resultA.ChallengeRedirectHost.ShouldBe(host.Idp1.BaseAddress.Host,
            "the host-resolved space on alias A must redirect straight to IdP 1's authorize endpoint, never IdP 2's");
        resultA.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        resultA.Issuer.ShouldBe(idp1Authority);

        using var hostBClient = SpacesFederationTestHost.CreateBrowserClientForOrigin(host.FederationServer, hostBOrigin);
        var resultB = await SpacesFederationTestHost.RunOidcSignInAsync(hostBClient, host.Idp2, urlPrefix: "");
        resultB.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host,
            "the host-resolved space on alias B must redirect straight to IdP 2's authorize endpoint, never IdP 1's");
        resultB.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        resultB.Issuer.ShouldBe(idp2Authority);

        resultA.Subject.ShouldNotBe(resultB.Subject);
        resultA.Issuer.ShouldNotBe(resultB.Issuer);
    }

    [Fact]
    public async Task concurrent_interleaved_sign_ins_to_both_spaces_each_land_on_the_correct_idp()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(concurrent_interleaved_sign_ins_to_both_spaces_each_land_on_the_correct_idp), _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);
        await host.CreateOidcProviderForLoginAsync(host.SpaceBId, idp2Authority, "client", _ct);

        const int iterations = 10;

        var spaceAClients = Enumerable.Range(0, iterations)
            .Select(_ => SpacesFederationTestHost.CreateBrowserClient(host.FederationServer)).ToArray();
        var spaceBClients = Enumerable.Range(0, iterations)
            .Select(_ => SpacesFederationTestHost.CreateBrowserClient(host.FederationServer)).ToArray();
        var idp1Clients = Enumerable.Range(0, iterations)
            .Select(_ => SpacesFederationTestHost.CreateBrowserClient(host.Idp1)).ToArray();
        var idp2Clients = Enumerable.Range(0, iterations)
            .Select(_ => SpacesFederationTestHost.CreateBrowserClient(host.Idp2)).ToArray();

        try
        {
            var (spaceAAuthorizeUrls, spaceBAuthorizeUrls) = await WhenAllBothSpacesAsync(
                i => SpacesFederationTestHost.ChallengeAsync(spaceAClients[i], "/t/space-a"),
                i => SpacesFederationTestHost.ChallengeAsync(spaceBClients[i], "/t/space-b"),
                iterations);

            for (var i = 0; i < iterations; i++)
            {
                new Uri(spaceAAuthorizeUrls[i]).Host.ShouldBe(host.Idp1.BaseAddress.Host);
                new Uri(spaceBAuthorizeUrls[i]).Host.ShouldBe(host.Idp2.BaseAddress.Host);
            }

            var (spaceARedirectUris, spaceBRedirectUris) = await WhenAllBothSpacesAsync(
                i => SpacesFederationTestHost.LoginAtIdpAsync(idp1Clients[i], spaceAAuthorizeUrls[i]),
                i => SpacesFederationTestHost.LoginAtIdpAsync(idp2Clients[i], spaceBAuthorizeUrls[i]),
                iterations);

            var (spaceAFinishUris, spaceBFinishUris) = await WhenAllBothSpacesAsync(
                i => SpacesFederationTestHost.CallbackAsync(spaceAClients[i], spaceARedirectUris[i]),
                i => SpacesFederationTestHost.CallbackAsync(spaceBClients[i], spaceBRedirectUris[i]),
                iterations);

            var (spaceAResults, spaceBResults) = await WhenAllBothSpacesAsync(
                i => SpacesFederationTestHost.FinishAsync(spaceAClients[i], spaceAFinishUris[i], spaceAAuthorizeUrls[i]),
                i => SpacesFederationTestHost.FinishAsync(spaceBClients[i], spaceBFinishUris[i], spaceBAuthorizeUrls[i]),
                iterations);

            foreach (var result in spaceAResults)
            {
                result.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
                result.Issuer.ShouldBe(idp1Authority);
            }

            foreach (var result in spaceBResults)
            {
                result.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
                result.Issuer.ShouldBe(idp2Authority);
            }
        }
        finally
        {
            foreach (var client in spaceAClients.Concat(spaceBClients).Concat(idp1Clients).Concat(idp2Clients))
            {
                client.Dispose();
            }
        }
    }

    [Fact]
    public async Task changing_a_provider_at_runtime_makes_the_next_sign_in_use_the_new_config_when_store_is_cached()
    {
        var cacheDuration = TimeSpan.FromMilliseconds(200);
        await using var host = await SpacesFederationTestHost.CreateWithCachingWrapperAsync(
            webServerFixture, nameof(changing_a_provider_at_runtime_makes_the_next_sign_in_use_the_new_config_when_store_is_cached),
            cacheDuration, _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        var providerId = await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);

        var firstResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        firstResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        firstResult.Issuer.ShouldBe(idp1Authority);

        await host.UpdateOidcProviderAuthorityAsync(host.SpaceAId, providerId, idp2Authority, _ct);

        // Wait for the caching store's model-level cache entry to expire.
        await Task.Delay(cacheDuration + TimeSpan.FromMilliseconds(300), _ct);

        var secondResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-a");
        secondResult.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host,
            "after repointing space 1's provider at IdP 2 and waiting past the cache duration, the next sign-in's challenge must redirect to IdP 2");
        secondResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        secondResult.Issuer.ShouldBe(idp2Authority);
    }

    [Fact]
    public async Task adding_a_provider_at_runtime_within_a_space_is_usable_immediately_when_store_is_cached()
    {
        var cacheDuration = TimeSpan.FromMilliseconds(200);
        await using var host = await SpacesFederationTestHost.CreateWithCachingWrapperAsync(
            webServerFixture, nameof(adding_a_provider_at_runtime_within_a_space_is_usable_immediately_when_store_is_cached),
            cacheDuration, _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        // Space 2's provider is created and used first.
        await host.CreateOidcProviderForLoginAsync(host.SpaceBId, idp2Authority, "client", _ct);
        var spaceBResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBResult.Issuer.ShouldBe(idp2Authority);

        // Space 1's provider is created for the first time, only now. It must be usable
        // immediately even under the caching wrapper, with no restart or warmup and no
        // wait, because there is no prior (stale) cache entry for this provider.
        await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);
        var spaceAResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        spaceAResult.ChallengeRedirectHost.ShouldBe(host.Idp1.BaseAddress.Host);
        spaceAResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        spaceAResult.Issuer.ShouldBe(idp1Authority);
    }

    [Fact]
    public async Task removing_a_provider_makes_it_unusable_then_re_adding_with_a_new_config_uses_the_new_config_when_store_is_cached()
    {
        var cacheDuration = TimeSpan.FromMilliseconds(200);
        await using var host = await SpacesFederationTestHost.CreateWithCachingWrapperAsync(
            webServerFixture,
            nameof(removing_a_provider_makes_it_unusable_then_re_adding_with_a_new_config_uses_the_new_config_when_store_is_cached),
            cacheDuration, _ct);

        var idp1Authority = host.Idp1Authority;
        var idp2Authority = host.Idp2Authority;

        var providerId = await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp1Authority, "client", _ct);
        await host.CreateOidcProviderForLoginAsync(host.SpaceBId, idp2Authority, "client", _ct);

        var firstResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        firstResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);

        await host.DeleteOidcProviderAsync(host.SpaceAId, providerId, _ct);

        // Wait for the caching store's model-level cache entry to expire before confirming
        // the deleted provider is unusable, otherwise the stale cached entry could still
        // resolve.
        await Task.Delay(cacheDuration + TimeSpan.FromMilliseconds(300), _ct);

        using (var mainClient = SpacesFederationTestHost.CreateBrowserClient(host.FederationServer))
        {
            await AssertChallengeIsUnreachableAfterDeleteAsync(mainClient, "/t/space-a/test/challenge", host.Idp1, host.Idp2);
        }

        // Space 2 is untouched by space 1's deletion.
        var spaceBStillWorks = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBStillWorks.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBStillWorks.Issuer.ShouldBe(idp2Authority);

        // Re-add space 1's provider with a DIFFERENT config (pointing at IdP 2), proving no
        // stale cached/tracked entry survives the delete.
        await host.CreateOidcProviderForLoginAsync(host.SpaceAId, idp2Authority, "client", _ct);

        // Wait again for the caching store's model-level cache entry to expire before
        // confirming the re-added provider's new config is used.
        await Task.Delay(cacheDuration + TimeSpan.FromMilliseconds(300), _ct);

        var reAddedResult = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-a");
        reAddedResult.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host);
        reAddedResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        reAddedResult.Issuer.ShouldBe(idp2Authority);

        // Space 2 remains unaffected throughout.
        var spaceBFinalCheck = await SpacesFederationTestHost.RunOidcSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBFinalCheck.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBFinalCheck.Issuer.ShouldBe(idp2Authority);
    }

    [Fact]
    public async Task saml_signing_in_to_space_one_then_space_two_each_lands_at_its_own_idp()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(saml_signing_in_to_space_one_then_space_two_each_lands_at_its_own_idp), _ct);

        await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceBId, "/t/space-b", host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);

        var resultA = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        resultA.ChallengeRedirectHost.ShouldBe(host.Idp1.BaseAddress.Host,
            "space A's challenge must redirect straight to IdP 1's SSO endpoint, never IdP 2's");
        resultA.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        resultA.Issuer.ShouldBe(host.Idp1SamlIssuer);

        var resultB = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        resultB.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host,
            "space B's challenge must redirect straight to IdP 2's SSO endpoint, never IdP 1's");
        resultB.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        resultB.Issuer.ShouldBe(host.Idp2SamlIssuer);

        // The two NameIDs and issuers are distinct: a space-2 challenge never reaches IdP 1,
        // and neither IdP's identity can be confused with the other's.
        resultA.Subject.ShouldNotBe(resultB.Subject);
        resultA.Issuer.ShouldNotBe(resultB.Issuer);
    }

    [Fact]
    public async Task saml_signing_out_of_space_one_sends_the_logout_request_to_its_own_idp()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(saml_signing_out_of_space_one_sends_the_logout_request_to_its_own_idp), _ct);

        await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceBId, "/t/space-b", host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);

        var logoutRequestRedirect = await SpacesFederationTestHost.RunSamlSignInThenLogoutAsync(
            host.FederationServer, host.Idp1, "/t/space-a");

        logoutRequestRedirect.Host.ShouldBe(host.Idp1.BaseAddress.Host,
            "space A's sign-out must send the logout request to IdP 1, never IdP 2");
        logoutRequestRedirect.Host.ShouldNotBe(host.Idp2.BaseAddress.Host);
        logoutRequestRedirect.AbsolutePath.ShouldBe("/Saml2/SLO");
    }

    [Fact]
    public async Task saml_signing_out_of_space_two_sends_the_logout_request_to_its_own_idp()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(saml_signing_out_of_space_two_sends_the_logout_request_to_its_own_idp), _ct);

        await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceBId, "/t/space-b", host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);

        var logoutRequestRedirect = await SpacesFederationTestHost.RunSamlSignInThenLogoutAsync(
            host.FederationServer, host.Idp2, "/t/space-b");

        logoutRequestRedirect.Host.ShouldBe(host.Idp2.BaseAddress.Host,
            "space B's sign-out must send the logout request to IdP 2, never IdP 1");
        logoutRequestRedirect.Host.ShouldNotBe(host.Idp1.BaseAddress.Host);
        logoutRequestRedirect.AbsolutePath.ShouldBe("/Saml2/SLO");
    }

    [Fact]
    public async Task a_browser_does_not_send_a_space_one_saml_session_to_space_two()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(a_browser_does_not_send_a_space_one_saml_session_to_space_two), _ct);

        await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);

        using var mainClient = SpacesFederationTestHost.CreateBrowserClient(host.FederationServer);
        var signInResult = await SpacesFederationTestHost.RunSamlSignInAsync(mainClient, host.Idp1, "/t/space-a");
        signInResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);

        // The browser's cookie path scoping keeps the space A session cookie out of space B
        // requests, so space B's authorize endpoint sees no session and redirects to login.
        // The server does not bind sessions to a space; a replayed space A cookie is accepted
        // by space B (#3685).
        var authorize = await mainClient.GetAsync(
            $"/t/space-b/connect/authorize?client_id={SpacesFederationTestHost.SpaceBClientId}&response_type=code&scope=openid" +
            "&redirect_uri=https%3A%2F%2Frp.example%2Fcallback&code_challenge=abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ" +
            "&code_challenge_method=S256&state=s1");

        authorize.StatusCode.ShouldBe(HttpStatusCode.SeeOther);
        authorize.Headers.Location!.ToString().ShouldNotStartWith("https://rp.example/callback");
        authorize.Headers.Location!.ToString().ShouldContain("/account/login");
    }

    [Fact]
    public async Task saml_changing_a_provider_at_runtime_makes_the_next_sign_in_use_the_new_config()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(saml_changing_a_provider_at_runtime_makes_the_next_sign_in_use_the_new_config), _ct);

        var providerId = await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);

        var firstResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        firstResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        firstResult.Issuer.ShouldBe(host.Idp1SamlIssuer);

        await host.UpdateSamlProviderIdpAsync(host.SpaceAId, providerId, host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);

        var secondResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-a");
        secondResult.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host,
            "after repointing space 1's provider at IdP 2, the next sign-in's challenge must redirect to IdP 2");
        secondResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        secondResult.Issuer.ShouldBe(host.Idp2SamlIssuer);
    }

    [Fact]
    public async Task saml_adding_a_provider_at_runtime_within_a_space_is_usable_immediately()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(saml_adding_a_provider_at_runtime_within_a_space_is_usable_immediately), _ct);

        // Space 2's provider is created and used first.
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceBId, "/t/space-b", host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);
        var spaceBResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBResult.Issuer.ShouldBe(host.Idp2SamlIssuer);

        // Space 1's provider is created for the first time, only now. It must be usable
        // immediately, with no restart or warmup.
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);
        var spaceAResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        spaceAResult.ChallengeRedirectHost.ShouldBe(host.Idp1.BaseAddress.Host);
        spaceAResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        spaceAResult.Issuer.ShouldBe(host.Idp1SamlIssuer);
    }

    [Fact]
    public async Task saml_removing_a_provider_makes_it_unusable_then_re_adding_with_a_new_config_uses_the_new_config()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture,
            nameof(saml_removing_a_provider_makes_it_unusable_then_re_adding_with_a_new_config_uses_the_new_config),
            _ct);

        var providerId = await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceBId, "/t/space-b", host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);

        var firstResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        firstResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);

        await host.DeleteSamlProviderAsync(host.SpaceAId, providerId, _ct);

        // Confirm space 1 is unusable: whatever the challenge produces, it must not be a
        // redirect to either IdP. Asserted precisely without pinning a specific status as
        // contractually required (mirrors the OIDC remove scenario).
        using (var mainClient = SpacesFederationTestHost.CreateBrowserClient(host.FederationServer))
        {
            await AssertChallengeIsUnreachableAfterDeleteAsync(
                mainClient, $"/t/space-a/test/challenge?scheme={SpacesFederationTestHost.SamlScheme}", host.Idp1, host.Idp2);
        }

        // Space 2 is untouched by space 1's deletion.
        var spaceBStillWorks = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBStillWorks.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBStillWorks.Issuer.ShouldBe(host.Idp2SamlIssuer);

        // Re-add space 1's provider with a DIFFERENT config (pointing at IdP 2), proving no
        // stale cached/tracked entry survives the delete.
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);
        var reAddedResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-a");
        reAddedResult.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host);
        reAddedResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        reAddedResult.Issuer.ShouldBe(host.Idp2SamlIssuer);

        // Space 2 remains unaffected throughout.
        var spaceBFinalCheck = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBFinalCheck.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBFinalCheck.Issuer.ShouldBe(host.Idp2SamlIssuer);
    }

    [Fact]
    public async Task saml_concurrent_interleaved_sign_ins_to_both_spaces_each_land_on_the_correct_idp()
    {
        await using var host = await SpacesFederationTestHost.CreateAsync(
            webServerFixture, nameof(saml_concurrent_interleaved_sign_ins_to_both_spaces_each_land_on_the_correct_idp), _ct);

        await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceBId, "/t/space-b", host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);

        var idp1Issuer = host.Idp1SamlIssuer;
        var idp2Issuer = host.Idp2SamlIssuer;

        const int iterations = 10;

        var spaceAClients = Enumerable.Range(0, iterations)
            .Select(_ => SpacesFederationTestHost.CreateBrowserClient(host.FederationServer)).ToArray();
        var spaceBClients = Enumerable.Range(0, iterations)
            .Select(_ => SpacesFederationTestHost.CreateBrowserClient(host.FederationServer)).ToArray();
        var idp1Clients = Enumerable.Range(0, iterations)
            .Select(_ => SpacesFederationTestHost.CreateBrowserClient(host.Idp1)).ToArray();
        var idp2Clients = Enumerable.Range(0, iterations)
            .Select(_ => SpacesFederationTestHost.CreateBrowserClient(host.Idp2)).ToArray();

        try
        {
            var (spaceASsoUrls, spaceBSsoUrls) = await WhenAllBothSpacesAsync(
                i => SpacesFederationTestHost.SamlChallengeAsync(spaceAClients[i], "/t/space-a"),
                i => SpacesFederationTestHost.SamlChallengeAsync(spaceBClients[i], "/t/space-b"),
                iterations);

            for (var i = 0; i < iterations; i++)
            {
                new Uri(spaceASsoUrls[i]).Host.ShouldBe(host.Idp1.BaseAddress.Host);
                new Uri(spaceBSsoUrls[i]).Host.ShouldBe(host.Idp2.BaseAddress.Host);
            }

            var (spaceAAutoLoginUrls, spaceBAutoLoginUrls) = await WhenAllBothSpacesAsync(
                i => SpacesFederationTestHost.SamlSsoRequestAsync(idp1Clients[i], spaceASsoUrls[i]),
                i => SpacesFederationTestHost.SamlSsoRequestAsync(idp2Clients[i], spaceBSsoUrls[i]),
                iterations);

            var (spaceASsoUrlsAgain, spaceBSsoUrlsAgain) = await WhenAllBothSpacesAsync(
                i => SpacesFederationTestHost.SamlAutoLoginAsync(idp1Clients[i], spaceAAutoLoginUrls[i]),
                i => SpacesFederationTestHost.SamlAutoLoginAsync(idp2Clients[i], spaceBAutoLoginUrls[i]),
                iterations);

            var (spaceAForms, spaceBForms) = await WhenAllBothSpacesAsync(
                i => SpacesFederationTestHost.SamlSsoResponseAsync(idp1Clients[i], spaceASsoUrlsAgain[i]),
                i => SpacesFederationTestHost.SamlSsoResponseAsync(idp2Clients[i], spaceBSsoUrlsAgain[i]),
                iterations);

            var (spaceAFinishUris, spaceBFinishUris) = await WhenAllBothSpacesAsync(
                i => SpacesFederationTestHost.SamlAcsAsync(spaceAClients[i], spaceAForms[i].AcsUrl, spaceAForms[i].FormData),
                i => SpacesFederationTestHost.SamlAcsAsync(spaceBClients[i], spaceBForms[i].AcsUrl, spaceBForms[i].FormData),
                iterations);

            var (spaceAResults, spaceBResults) = await WhenAllBothSpacesAsync(
                i => SpacesFederationTestHost.FinishAsync(spaceAClients[i], spaceAFinishUris[i], spaceASsoUrls[i]),
                i => SpacesFederationTestHost.FinishAsync(spaceBClients[i], spaceBFinishUris[i], spaceBSsoUrls[i]),
                iterations);

            foreach (var result in spaceAResults)
            {
                result.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
                result.Issuer.ShouldBe(idp1Issuer);
            }

            foreach (var result in spaceBResults)
            {
                result.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
                result.Issuer.ShouldBe(idp2Issuer);
            }
        }
        finally
        {
            foreach (var client in spaceAClients.Concat(spaceBClients).Concat(idp1Clients).Concat(idp2Clients))
            {
                client.Dispose();
            }
        }
    }

    [Fact]
    public async Task saml_changing_a_provider_at_runtime_makes_the_next_sign_in_use_the_new_config_when_store_is_cached()
    {
        var cacheDuration = TimeSpan.FromMilliseconds(200);
        await using var host = await SpacesFederationTestHost.CreateWithCachingWrapperAsync(
            webServerFixture,
            nameof(saml_changing_a_provider_at_runtime_makes_the_next_sign_in_use_the_new_config_when_store_is_cached),
            cacheDuration, _ct);

        var providerId = await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);

        var firstResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        firstResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        firstResult.Issuer.ShouldBe(host.Idp1SamlIssuer);

        await host.UpdateSamlProviderIdpAsync(host.SpaceAId, providerId, host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);

        // Wait for the caching store's model-level cache entry to expire.
        await Task.Delay(cacheDuration + TimeSpan.FromMilliseconds(300), _ct);

        var secondResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-a");
        secondResult.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host,
            "after repointing space 1's provider at IdP 2 and waiting past the cache duration, the next sign-in's challenge must redirect to IdP 2");
        secondResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        secondResult.Issuer.ShouldBe(host.Idp2SamlIssuer);
    }

    [Fact]
    public async Task saml_adding_a_provider_at_runtime_within_a_space_is_usable_immediately_when_store_is_cached()
    {
        var cacheDuration = TimeSpan.FromMilliseconds(200);
        await using var host = await SpacesFederationTestHost.CreateWithCachingWrapperAsync(
            webServerFixture,
            nameof(saml_adding_a_provider_at_runtime_within_a_space_is_usable_immediately_when_store_is_cached),
            cacheDuration, _ct);

        // Space 2's provider is created and used first.
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceBId, "/t/space-b", host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);
        var spaceBResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBResult.Issuer.ShouldBe(host.Idp2SamlIssuer);

        // Space 1's provider is created for the first time, only now. It must be usable
        // immediately even under the caching wrapper, with no restart or warmup and no
        // wait, because there is no prior (stale) cache entry for this provider.
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);
        var spaceAResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        spaceAResult.ChallengeRedirectHost.ShouldBe(host.Idp1.BaseAddress.Host);
        spaceAResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);
        spaceAResult.Issuer.ShouldBe(host.Idp1SamlIssuer);
    }

    [Fact]
    public async Task saml_removing_a_provider_makes_it_unusable_then_re_adding_with_a_new_config_uses_the_new_config_when_store_is_cached()
    {
        var cacheDuration = TimeSpan.FromMilliseconds(200);
        await using var host = await SpacesFederationTestHost.CreateWithCachingWrapperAsync(
            webServerFixture,
            nameof(saml_removing_a_provider_makes_it_unusable_then_re_adding_with_a_new_config_uses_the_new_config_when_store_is_cached),
            cacheDuration, _ct);

        var providerId = await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp1, host.Idp1SamlSigningCertificateBase64, _ct);
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceBId, "/t/space-b", host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);

        var firstResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp1, "/t/space-a");
        firstResult.Subject.ShouldBe(SpacesFederationTestHost.Idp1Subject);

        await host.DeleteSamlProviderAsync(host.SpaceAId, providerId, _ct);

        // Wait for the caching store's model-level cache entry to expire before confirming
        // the deleted provider is unusable, otherwise the stale cached entry could still
        // resolve.
        await Task.Delay(cacheDuration + TimeSpan.FromMilliseconds(300), _ct);

        using (var mainClient = SpacesFederationTestHost.CreateBrowserClient(host.FederationServer))
        {
            await AssertChallengeIsUnreachableAfterDeleteAsync(
                mainClient, $"/t/space-a/test/challenge?scheme={SpacesFederationTestHost.SamlScheme}", host.Idp1, host.Idp2);
        }

        // Space 2 is untouched by space 1's deletion.
        var spaceBStillWorks = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBStillWorks.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBStillWorks.Issuer.ShouldBe(host.Idp2SamlIssuer);

        // Re-add space 1's provider with a DIFFERENT config (pointing at IdP 2), proving no
        // stale cached/tracked entry survives the delete.
        await host.CreateSamlProviderForLoginAsync(
            host.SpaceAId, "/t/space-a", host.Idp2, host.Idp2SamlSigningCertificateBase64, _ct);

        // Wait again for the caching store's model-level cache entry to expire before
        // confirming the re-added provider's new config is used.
        await Task.Delay(cacheDuration + TimeSpan.FromMilliseconds(300), _ct);

        var reAddedResult = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-a");
        reAddedResult.ChallengeRedirectHost.ShouldBe(host.Idp2.BaseAddress.Host);
        reAddedResult.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        reAddedResult.Issuer.ShouldBe(host.Idp2SamlIssuer);

        // Space 2 remains unaffected throughout.
        var spaceBFinalCheck = await SpacesFederationTestHost.RunSamlSignInAsync(host.FederationServer, host.Idp2, "/t/space-b");
        spaceBFinalCheck.Subject.ShouldBe(SpacesFederationTestHost.Idp2Subject);
        spaceBFinalCheck.Issuer.ShouldBe(host.Idp2SamlIssuer);
    }
}
