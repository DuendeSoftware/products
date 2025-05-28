// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class ClientInfoDiagnosticEntryTests
{
    [Fact]
    public async Task Handles_No_Clients_Loaded()
    {
        var clientStore = new InMemoryClientStore([]);
        var logger = new NullLogger<ClientInfoDiagnosticEntry>();
        var subject = new ClientInfoDiagnosticEntry(clientStore, logger);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        result.RootElement.GetProperty("Clients").EnumerateObject().Count().ShouldBe(0);
    }

    [Fact]
    public async Task Does_Not_Write_Client_Secrets()
    {
        var client = new Client
        {
            ClientId = "test-client",
            ClientName = "Test Client",
            ClientSecrets = new List<Secret> { new Secret("secret".Sha256()) }
        };
        var resetEvent = new AutoResetEvent(false);
        var clientStore = new TestingClientStore([client], resetEvent);
        var logger = new NullLogger<ClientInfoDiagnosticEntry>();
        var subject = new ClientInfoDiagnosticEntry(clientStore, logger);

        Duende.IdentityServer.Telemetry.Metrics.ClientLoaded(client.ClientId);

        resetEvent.WaitOne();

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var clientsElement = result.RootElement.GetProperty("Clients");
        clientsElement.TryGetProperty("test-client", out var clientElement).ShouldBeTrue();
        clientElement.GetProperty("ClientName").GetString().ShouldBe("Test Client");
        clientElement.TryGetProperty("ClientSecrets", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Handles_Single_Client_Loaded()
    {
        var client = new Client
        {
            ClientId = "test-client",
            ClientName = "Test Client"
        };
        var resetEvent = new AutoResetEvent(false);
        var clientStore = new TestingClientStore([client], resetEvent);
        var logger = new NullLogger<ClientInfoDiagnosticEntry>();
        var subject = new ClientInfoDiagnosticEntry(clientStore, logger);

        Duende.IdentityServer.Telemetry.Metrics.ClientLoaded(client.ClientId);

        resetEvent.WaitOne();

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        result.RootElement.GetProperty("Clients").TryGetProperty("test-client", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Handles_Multiple_Clients_Loaded()
    {
        var client1 = new Client
        {
            ClientId = "test-client-1",
            ClientName = "Test Client 1"
        };
        var client2 = new Client
        {
            ClientId = "test-client-2",
            ClientName = "Test Client 2"
        };
        var resetEvent = new AutoResetEvent(false);
        var clientStore = new TestingClientStore([client1, client2], resetEvent);
        var logger = new NullLogger<ClientInfoDiagnosticEntry>();
        var subject = new ClientInfoDiagnosticEntry(clientStore, logger);

        Duende.IdentityServer.Telemetry.Metrics.ClientLoaded(client1.ClientId);
        resetEvent.WaitOne();

        Duende.IdentityServer.Telemetry.Metrics.ClientLoaded(client2.ClientId);
        resetEvent.WaitOne();

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        result.RootElement.GetProperty("Clients").TryGetProperty("test-client-1", out _).ShouldBeTrue();
        result.RootElement.GetProperty("Clients").TryGetProperty("test-client-2", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Handles_Same_Client_Loaded_Multiple_Times()
    {
        var client = new Client
        {
            ClientId = "test-client",
            ClientName = "Test Client"
        };
        var resetEvent = new AutoResetEvent(false);
        var clientStore = new TestingClientStore([client], resetEvent);
        var logger = new NullLogger<ClientInfoDiagnosticEntry>();
        var subject = new ClientInfoDiagnosticEntry(clientStore, logger);

        Duende.IdentityServer.Telemetry.Metrics.ClientLoaded(client.ClientId);

        resetEvent.WaitOne();

        Duende.IdentityServer.Telemetry.Metrics.ClientLoaded(client.ClientId);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        result.RootElement.GetProperty("Clients").TryGetProperty("test-client", out _).ShouldBeTrue();
    }

    private class TestingClientStore(IEnumerable<Client> clients, AutoResetEvent resetEvent) : IClientStore
    {
        private readonly IClientStore _inner = new InMemoryClientStore(clients);

        public async Task<Client?> FindClientByIdAsync(string clientId)
        {
            var client = await _inner.FindClientByIdAsync(clientId);
            resetEvent.Set();

            return client;
        }
    }
}
