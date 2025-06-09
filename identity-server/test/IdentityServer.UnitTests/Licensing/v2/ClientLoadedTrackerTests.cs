// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2.Diagnostics;
using Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Licensing.V2;

public class ClientLoadedTrackerTests
{
    [Fact]
    public void Should_Include_Only_Client_Secret_Types_For_Tracked_Client()
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            ClientSecrets =
            [
                new Secret { Type = "SharedSecret", Value = "Test" },
                new Secret { Type = "X509", Value = "Test2" }
            ]
        };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);

        var clientDetails = subject.Clients.FirstOrDefault(client => client.Key == testClient.ClientId);
        clientDetails.Value.TryGetPropertyValue("SecretTypes", out var secretTypes);
        secretTypes?.AsArray().Select(x => x.GetValue<string>()).ShouldBe(["SharedSecret", "X509"]);
    }

    [Fact]
    public void Should_Exclude_Properties_From_Tracked_Client()
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            Properties = { ["TestProperty"] = "TestValue" },
            LogoUri = "https://example.com/logo.png",
            Claims =
            [
                new ClientClaim("custom_claim", "claim_value")
            ]
        };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);

        var clientDetails = subject.Clients.FirstOrDefault(client => client.Key == testClient.ClientId);
        clientDetails.Value.TryGetPropertyValue("TestProperty", out _).ShouldBeFalse();
        clientDetails.Value.TryGetPropertyValue("LogoUri", out _).ShouldBeFalse();
        clientDetails.Value.TryGetPropertyValue("Claims", out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_Limit_Clients_Tracked()
    {
        var subject = new ClientLoadedTracker();

        for (var i = 0; i < 105; i++)
        {
            var testClient = new Client { ClientId = $"test_client_{i}" };
            subject.TrackClientLoaded(testClient);
        }

        subject.Clients.Count.ShouldBe(100);
    }

    [Fact]
    public void Should_Exclude_Empty_Array_Properties()
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            ClientSecrets = []
        };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);

        var clientDetails = subject.Clients.FirstOrDefault(client => client.Key == testClient.ClientId);
        clientDetails.Value.TryGetPropertyValue("ClientSecrets", out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_Exclude_Null_Values()
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            ClientName = null
        };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);

        var clientDetails = subject.Clients.FirstOrDefault(client => client.Key == testClient.ClientId);
        clientDetails.Value.TryGetPropertyValue("ClientName", out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_Restrict_Length_Of_Array_Properties()
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            AllowedScopes = Enumerable.Range(1, 20).Select(i => $"scope_{i}").ToList()
        };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);

        var clientDetails = subject.Clients.FirstOrDefault(client => client.Key == testClient.ClientId);
        clientDetails.Value.TryGetPropertyValue("AllowedScopes", out var allowedScopes);
        allowedScopes?.AsArray().Count.ShouldBe(10);
    }

    [Fact]
    public void Should_Handle_String_Property_Correctly()
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            ClientName = "Test Client"
        };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);

        var clientDetails = subject.Clients.FirstOrDefault(client => client.Key == testClient.ClientId);
        clientDetails.Value.TryGetPropertyValue("ClientName", out var clientName);
        clientName?.GetValue<string>().ShouldBe("Test Client");
    }

    [Fact]
    public void Should_Handle_Boolean_Property_Correctly()
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            Enabled = true
        };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);

        var clientDetails = subject.Clients.FirstOrDefault(client => client.Key == testClient.ClientId);
        clientDetails.Value.TryGetPropertyValue("Enabled", out var enabled);
        enabled?.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void Should_Handle_Enum_Property_Correctly()
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            AccessTokenType = AccessTokenType.Reference
        };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);

        var clientDetails = subject.Clients.FirstOrDefault(client => client.Key == testClient.ClientId);
        clientDetails.Value.TryGetPropertyValue("AccessTokenType", out var accessTokenType).ShouldBeTrue();
        accessTokenType?.GetValue<string>().ShouldBe(nameof(AccessTokenType.Reference));
    }

    [Fact]
    public void Should_Handle_TimeSpan_Property_Correctly()
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            DPoPClockSkew = TimeSpan.FromDays(30)
        };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);

        var clientDetails = subject.Clients.FirstOrDefault(client => client.Key == testClient.ClientId);
        clientDetails.Value.TryGetPropertyValue("AbsoluteRefreshTokenLifetime", out var absoluteRefreshTokenLifetime);
        absoluteRefreshTokenLifetime?.GetValue<string>().ShouldBe(TimeSpan.FromDays(30).ToString());
    }

    [Fact]
    public void Should_Handle_Int_Property_Correctly()
    {
        var testClient = new Client
        {
            ClientId = "test_client",
            AbsoluteRefreshTokenLifetime = 3600
        };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);

        var clientDetails = subject.Clients.FirstOrDefault(client => client.Key == testClient.ClientId);
        clientDetails.Value.TryGetPropertyValue("AbsoluteRefreshTokenLifetime", out var absoluteRefreshTokenLifetime);
        absoluteRefreshTokenLifetime?.GetValue<double>().ShouldBe(3600);
    }

    [Fact]
    public void Should_Not_Track_Client_When_Already_Tracked()
    {
        var testClient = new Client { ClientId = "test_client" };
        var subject = new ClientLoadedTracker();

        subject.TrackClientLoaded(testClient);
        subject.TrackClientLoaded(testClient);

        subject.Clients.Count.ShouldBe(1);
        subject.Clients.ContainsKey(testClient.ClientId).ShouldBeTrue();
    }
}
