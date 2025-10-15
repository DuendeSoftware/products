// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.IntegrationTests.Extensibility;

public class CustomAuthorizeResponseGeneratorTests
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private const string Category = "CustomAuthorizeResponseGeneratorTests";

    private IdentityServerPipeline _mockPipeline = new IdentityServerPipeline();

    public CustomAuthorizeResponseGeneratorTests()
    {
        _mockPipeline.OnPostConfigureServices += svcs =>
        {
            svcs.AddTransient<IAuthorizeResponseGenerator, CustomAuthorizeResponseGenerator>();
        };

        _mockPipeline.Clients.Add(new Client
        {
            ClientId = "test",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.Code,
            RedirectUris = { "https://client1/callback" },
            AllowedScopes = { "scope1", "openid", "profile" }
        });

        _mockPipeline.IdentityScopes.AddRange([
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
            new IdentityResources.Email()
        ]);
        _mockPipeline.ApiScopes.Add(new ApiScope("scope1"));
        _mockPipeline.ApiResources.Add(new ApiResource("urn:res1") { Scopes = { "scope1" } });

        _mockPipeline.Users.Add(new Test.TestUser
        {
            SubjectId = "bob",
            Username = "bob",
            Password = "password",
        });

        _mockPipeline.Initialize();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task custom_parameter_should_be_in_authorize_response()
    {
        _mockPipeline.Subject = new IdentityServerUser("bob").CreatePrincipal();
        _mockPipeline.BrowserClient.StopRedirectingAfter = 2;

        var url = _mockPipeline.CreateAuthorizeUrl(
            clientId: "test",
            responseType: "code",
            scope: "openid profile scope1",
            redirectUri: "https://client1/callback",
            codeChallenge: new string('a', _mockPipeline.Options.InputLengthRestrictions.CodeVerifierMinLength),
            codeChallengeMethod: OidcConstants.CodeChallengeMethods.Sha256,
            state: "123_state",
            nonce: "123_nonce");
        var response = await _mockPipeline.BrowserClient.GetAsync(url, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.ToString().ShouldStartWith("https://client1/callback");

        var authorization = new Duende.IdentityModel.Client.AuthorizeResponse(response.Headers.Location.ToString());
        authorization.IsError.ShouldBeFalse();
        authorization.Values.ShouldContainKeyAndValue("custom_parameter", "custom_value");
    }
}

public class CustomAuthorizeResponseGenerator(
    IdentityServerOptions options,
    IClock clock,
    ITokenService tokenService,
    IKeyMaterialService keyMaterialService,
    IAuthorizationCodeStore authorizationCodeStore,
    ILogger<AuthorizeResponseGenerator> logger,
    IEventService events)
    : AuthorizeResponseGenerator(options, clock, tokenService, keyMaterialService, authorizationCodeStore, logger,
        events)
{
    public override async Task<AuthorizeResponse> CreateResponseAsync(ValidatedAuthorizeRequest request)
    {
        var baseResponse = await base.CreateResponseAsync(request).ConfigureAwait(false);
        if (!baseResponse.IsError)
        {
            baseResponse.CustomParameters.Add("custom_parameter", "custom_value");
        }

        return baseResponse;
    }
}
