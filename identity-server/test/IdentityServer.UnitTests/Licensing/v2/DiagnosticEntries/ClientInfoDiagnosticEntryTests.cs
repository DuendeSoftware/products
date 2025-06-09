// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class ClientInfoDiagnosticEntryTests
{
    [Fact]
    public async Task Should_Write_Client_Info()
    {
        var clientLoadedTracker = new ClientLoadedTracker();
        var testClient = new Client
        {
            ClientId = "test_client",
            ClientSecrets =
            [
                new Secret { Type = "SharedSecret", Value = "Test" },
                new Secret { Type = "X509", Value = "Test2" }
            ],
            AllowOfflineAccess = true,
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AccessTokenLifetime = 60,
            AllowedScopes = ["api1", "api2"],
            AccessTokenType = AccessTokenType.Reference
        };
        clientLoadedTracker.TrackClientLoaded(testClient);
        var subject = new ClientInfoDiagnosticEntry(clientLoadedTracker);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var clientInfo = result.RootElement.GetProperty("Clients");
        clientInfo.GetArrayLength().ShouldBe(1);
        var client = clientInfo[0];
        client.GetProperty("ClientId").GetString().ShouldBe("test_client");
        client.TryGetStringArray("SecretTypes").ShouldBe(["SharedSecret", "X509"]);
        client.GetProperty("AllowOfflineAccess").GetBoolean().ShouldBeTrue();
        client.TryGetStringArray("AllowedGrantTypes").ShouldBe(GrantTypes.ClientCredentials);
        client.GetProperty("AccessTokenLifetime").GetInt32().ShouldBe(60);
        client.TryGetStringArray("AllowedScopes").ShouldBe(["api1", "api2"]);
        client.GetProperty("AccessTokenType").GetString().ShouldBe(nameof(AccessTokenType.Reference));
    }

    [Fact]
    public async Task Should_Write_Empty_Client_Info_When_No_Clients_Tracked()
    {
        var clientLoadedTracker = new ClientLoadedTracker();
        var subject = new ClientInfoDiagnosticEntry(clientLoadedTracker);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var clientInfo = result.RootElement.GetProperty("Clients");
        clientInfo.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_Write_Multiple_Clients()
    {
        var clientLoadedTracker = new ClientLoadedTracker();
        for (var i = 0; i < 5; i++)
        {
            var testClient = new Client { ClientId = $"test_client_{i}" };
            clientLoadedTracker.TrackClientLoaded(testClient);
        }
        var subject = new ClientInfoDiagnosticEntry(clientLoadedTracker);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var clientInfo = result.RootElement.GetProperty("Clients");
        clientInfo.GetArrayLength().ShouldBe(5);
    }
}
