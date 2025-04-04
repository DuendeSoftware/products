// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using UnitTests.Common;
using UnitTests.Services.Default.KeyManagement;
using UnitTests.Validation.Setup;

namespace UnitTests.Services.Default;

public class DefaultBackChannelLogoutServiceTests
{
    private class ServiceTestHarness : DefaultBackChannelLogoutService
    {
        public ServiceTestHarness(
            IClock clock,
            IIdentityServerTools tools,
            ILogoutNotificationService logoutNotificationService,
            IBackChannelLogoutHttpClient backChannelLogoutHttpClient,
            IIssuerNameService issuerNameService,
            ILogger<IBackChannelLogoutService> logger)
            : base(clock, tools, logoutNotificationService, backChannelLogoutHttpClient, issuerNameService, logger)
        {
        }


        // CreateTokenAsync is protected, so we use this wrapper to exercise it in our tests
        public async Task<string> ExerciseCreateTokenAsync(BackChannelLogoutRequest request) => await CreateTokenAsync(request);
    }

    [Fact]
    public async Task CreateTokenAsync_Should_Set_Issuer_Correctly()
    {
        var expected = "https://identity.example.com";

        var mockKeyMaterialService = new MockKeyMaterialService();
        var signingKey = new SigningCredentials(CryptoHelper.CreateRsaSecurityKey(), CryptoHelper.GetRsaSigningAlgorithmValue(IdentityServerConstants.RsaSigningAlgorithm.RS256));
        mockKeyMaterialService.SigningCredentials.Add(signingKey);

        var tokenCreation = new DefaultTokenCreationService(new MockClock(), mockKeyMaterialService, TestIdentityServerOptions.Create(), TestLogger.Create<DefaultTokenCreationService>());

        var issuerNameService = new TestIssuerNameService(expected);
        var tools = new IdentityServerTools(
            issuerNameService,
            tokenCreation,
            new MockClock(),
            TestIdentityServerOptions.Create()
        );

        var subject = new ServiceTestHarness(null, tools, null, null, issuerNameService, null);
        var rawToken = await subject.ExerciseCreateTokenAsync(new BackChannelLogoutRequest
        {
            ClientId = "test_client",
            SubjectId = "test_sub",
        });


        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64Url.Decode(rawToken.Split('.')[1]));
        payload["iss"].GetString().ShouldBe(expected);
    }
}
