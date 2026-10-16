// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Specialized;
using System.Security.Cryptography;
using Duende.IdentityModel;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

// Values protected by IdentityServer's own consumers are bound to the space they were issued in. These tests
// call each consumer's unprotect path directly through the real registered services, so they cover the
// Unprotect boundary and not the endpoint-level replay.
public sealed class SpacesConsumerDataProtectionRejectionTests(WebServerFixture webServerFixture)
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task pushed_authorization_parameters_serialized_in_space_a_deserialize_in_space_a_and_throw_in_space_b()
    {
        await using var host = await CreateHostAsync(serverSideSessions: false);
        var raw = new NameValueCollection { ["client_id"] = "client", ["scope"] = "openid" };

        var serialized = await host.RunInSpaceAsync(host.SpaceA.Id,
            services => Task.FromResult(services.GetRequiredService<IPushedAuthorizationSerializer>().Serialize(raw)));

        var sameSpace = await host.RunInSpaceAsync(host.SpaceA.Id,
            services => Task.FromResult(services.GetRequiredService<IPushedAuthorizationSerializer>().Deserialize(serialized)));
        sameSpace["client_id"].ShouldBe("client");
        sameSpace["scope"].ShouldBe("openid");

        _ = await Should.ThrowAsync<CryptographicException>(() => host.RunInSpaceAsync(host.SpaceB.Id,
            services => Task.FromResult(services.GetRequiredService<IPushedAuthorizationSerializer>().Deserialize(serialized))));
    }

    [Fact]
    public async Task message_ids_written_in_space_a_are_read_in_space_a_and_null_in_space_b()
    {
        await using var host = await CreateHostAsync(serverSideSessions: false);
        var message = new Message<ErrorMessage>(new ErrorMessage { Error = "boom" }, DateTime.UtcNow);

        var id = await host.RunInSpaceAsync(host.SpaceA.Id,
            services => services.GetRequiredService<IMessageStore<ErrorMessage>>().WriteAsync(message, _ct));
        id.ShouldNotBeNullOrEmpty();

        var sameSpace = await host.RunInSpaceAsync(host.SpaceA.Id,
            services => services.GetRequiredService<IMessageStore<ErrorMessage>>().ReadAsync(id, _ct));
        sameSpace.ShouldNotBeNull();
        sameSpace.Data.Error.ShouldBe("boom");

        var otherSpace = await host.RunInSpaceAsync(host.SpaceB.Id,
            services => services.GetRequiredService<IMessageStore<ErrorMessage>>().ReadAsync(id, _ct));
        // The store keeps nothing: the id is the protected payload itself, so the read in space B has no
        // storage to miss and null can only come from the Unprotect failure.
        otherSpace.ShouldBeNull("ProtectedDataMessageStore returns null when it cannot unprotect the id");
    }

    [Fact]
    public async Task dpop_server_nonce_created_in_space_a_reads_back_in_space_a_and_as_zero_in_space_b()
    {
        await using var host = await CreateHostAsync(serverSideSessions: false);

        // The nonce helpers of DefaultDPoPProofValidator are protected, so a subclass built from the space's
        // services exposes them. This is the validator's real nonce path, not a copy of its purpose string.
        var nonce = await host.RunInSpaceAsync(host.SpaceA.Id,
            services => Task.FromResult(CreateValidator(services).CreateNonceValue()));

        var sameSpace = await host.RunInSpaceAsync(host.SpaceA.Id,
            services => CreateValidator(services).ReadNonceTimeAsync(nonce));
        sameSpace.ShouldBeGreaterThan(0);

        var otherSpace = await host.RunInSpaceAsync(host.SpaceB.Id,
            services => CreateValidator(services).ReadNonceTimeAsync(nonce));
        otherSpace.ShouldBe(0, "an undecryptable nonce reads as time 0, which the validator treats as expired");
    }

    [Fact]
    public async Task session_ticket_stored_in_space_a_is_retrieved_in_space_a_and_not_in_space_b()
    {
        await using var host = await CreateHostAsync(serverSideSessions: true);

        var (key, stored) = await host.RunInSpaceAsync(host.SpaceA.Id, async services =>
        {
            SpacesServerSideSessionTestHelpers.UseRequestContext(services);
            var ticketStore = services.GetRequiredService<IServerSideTicketStore>();
            var storedKey = await ticketStore.StoreAsync(SpacesServerSideSessionTestHelpers.CreateTicket());
            var session = await services.GetRequiredService<IServerSideSessionStore>().GetSessionAsync(storedKey, _ct);
            return (storedKey, session!);
        });

        var sameSpace = await host.RunInSpaceAsync(host.SpaceA.Id,
            services =>
            {
                SpacesServerSideSessionTestHelpers.UseRequestContext(services);
                return services.GetRequiredService<IServerSideTicketStore>().RetrieveAsync(key);
            });
        sameSpace.ShouldNotBeNull();
        sameSpace.Principal.FindFirst(JwtClaimTypes.Subject)?.Value.ShouldBe("alice");

        // Session storage is partitioned per space, so space B would simply not find the key and the result
        // would say nothing about data protection. Copying the stored protected ticket into space B's pool
        // makes B find the record, so the only thing that can reject it is the ticket protector.
        var retrievedInB = await host.RunInSpaceAsync(host.SpaceB.Id, async services =>
        {
            SpacesServerSideSessionTestHelpers.UseRequestContext(services);
            await services.GetRequiredService<IServerSideSessionStore>().CreateSessionAsync(new ServerSideSession
            {
                Key = key,
                Scheme = stored.Scheme,
                Created = stored.Created,
                Renewed = stored.Renewed,
                Expires = stored.Expires,
                SubjectId = stored.SubjectId,
                SessionId = stored.SessionId,
                DisplayName = stored.DisplayName,
                Ticket = stored.Ticket
            }, _ct);

            var copied = await services.GetRequiredService<IServerSideSessionStore>().GetSessionAsync(key, _ct);
            copied.ShouldNotBeNull("the copied record must be present in space B's pool");

            return await services.GetRequiredService<IServerSideTicketStore>().RetrieveAsync(key);
        });
        retrievedInB.ShouldBeNull("ServerSideTicketStore returns null when the ticket cannot be unprotected");
    }

    private static NonceValidator CreateValidator(IServiceProvider services) =>
        ActivatorUtilities.CreateInstance<NonceValidator>(services);

    private Task<SpacesDataProtectionTestHost> CreateHostAsync(bool serverSideSessions) =>
        SpacesDataProtectionTestHost.CreateAsync(webServerFixture, nameof(SpacesConsumerDataProtectionRejectionTests),
            new SpacesDataProtectionTestHostOptions { ServerSideSessions = serverSideSessions }, _ct);

    private sealed class NonceValidator(
        Duende.IdentityServer.Configuration.IdentityServerOptions options,
        IReplayCache replayCache,
        TimeProvider timeProvider,
        IDataProtectionProvider dataProtectionProvider)
        : DefaultDPoPProofValidator(options, replayCache, timeProvider, dataProtectionProvider, NullLogger<DefaultDPoPProofValidator>.Instance)
    {
        public string CreateNonceValue() =>
            CreateNonce(new DPoPProofValidationContext(), new DPoPProofValidationResult());

        public async Task<long> ReadNonceTimeAsync(string nonce) =>
            await GetUnixTimeFromNonceAsync(new DPoPProofValidationContext(), new DPoPProofValidationResult { Nonce = nonce });
    }
}
