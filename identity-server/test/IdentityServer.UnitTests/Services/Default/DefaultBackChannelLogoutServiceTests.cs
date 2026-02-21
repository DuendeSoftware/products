// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Buffers.Text;
using System.Text.Json;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace UnitTests.Services.Default;

public class DefaultBackChannelLogoutServiceTests
{
    private readonly CT _ct = TestContext.Current.CancellationToken;
    private class ServiceTestHarness : DefaultBackChannelLogoutService
    {
        public ServiceTestHarness(
            TimeProvider clock,
            IIdentityServerTools tools,
            ILogoutNotificationService logoutNotificationService,
            IBackChannelLogoutHttpClient backChannelLogoutHttpClient,
            IIssuerNameService issuerNameService,
            ILogger<IBackChannelLogoutService> logger)
            : base(clock, tools, logoutNotificationService, backChannelLogoutHttpClient, issuerNameService, logger)
        {
        }


        // CreateTokenAsync is protected, so we use this wrapper to exercise it in our tests
        public async Task<string> ExerciseCreateTokenAsync(BackChannelLogoutRequest request, CT ct) => await CreateTokenAsync(request, ct);
    }

    [Fact]
    public async Task CreateTokenAsync_Should_Set_Issuer_Correctly()
    {
        var expected = "https://identity.example.com";

        var mockKeyMaterialService = new MockKeyMaterialService();
        var signingKey = new SigningCredentials(CryptoHelper.CreateRsaSecurityKey(), CryptoHelper.GetRsaSigningAlgorithmValue(IdentityServerConstants.RsaSigningAlgorithm.RS256));
        mockKeyMaterialService.SigningCredentials.Add(signingKey);

        var tokenCreation = new DefaultTokenCreationService(new FakeTimeProvider(), mockKeyMaterialService, TestIdentityServerOptions.Create(), TestLogger.Create<DefaultTokenCreationService>());

        var issuerNameService = new TestIssuerNameService(expected);
        var tools = new IdentityServerTools(
            issuerNameService,
            tokenCreation,
            new FakeTimeProvider(),
            TestIdentityServerOptions.Create()
        );

        var subject = new ServiceTestHarness(null, tools, null, null, issuerNameService, null);
        var rawToken = await subject.ExerciseCreateTokenAsync(new BackChannelLogoutRequest
        {
            ClientId = "test_client",
            SubjectId = "test_sub",
        }, _ct);


        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64Url.DecodeFromChars(rawToken.Split('.')[1]));
        payload["iss"].GetString().ShouldBe(expected);
    }
}
