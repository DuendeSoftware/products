// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Duende.IdentityServer;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiResources;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.UserManagement;
using Duende.Spaces;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Scim;
using Duende.UserManagement.Scim.Internal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace IdentityServer8.UnitTests;

/// <summary>
/// End-to-end acceptance tests for EV-4132 (SCIM authentication with Spaces). These tests encode the
/// TARGET behavior described in <c>.weave/plans/scim-spaces-auth.md</c>: most are expected to FAIL on
/// current code (before Tasks 3-6 land), either with a 401/403 that doesn't match the target matrix, or
/// with a startup exception whose message doesn't yet mention the target wording. See the PR/task report
/// for the exact per-test result on current code.
///
/// Hosted on a plain <see cref="WebApplication"/> over <see cref="TestServer"/>, not
/// <c>KestrelBasedTestServer</c>: that harness's <c>WebAppWrapper</c> calls <c>UseRouting()</c> in its
/// constructor, before the test's pipeline delegate runs, so <c>UseSpaceResolution()</c> always ends up
/// after routing and space-prefixed SCIM endpoints (<c>/t/{space}/scim/...</c>) 404 regardless of the auth
/// configuration under test.
/// </summary>
public sealed class ScimSpacesAuthenticationTests
{
    private static Ct Ct => TestContext.Current.CancellationToken;

    private ITestOutputHelper Output => TestContext.Current.TestOutputHelper!;

    private const string SpaceAPath = "/space-a";
    private const string SpaceBPath = "/space-b";

    private const string KeepDefaultAccessTokenJwtType = "__keep-default__";

    private async Task<WebApplication> CreateSpacesScimServerAsync(
        bool sharedKey = false,
        string? accessTokenJwtType = KeepDefaultAccessTokenJwtType)
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();

        _ = TestUserManagementLicense.Create().Register(builder.Services, TimeProvider.System);

        var identityServerBuilder = builder.Services.AddIdentityServer(o =>
            {
                if (sharedKey)
                {
                    o.KeyManagement.Enabled = false;
                }

                if (!ReferenceEquals(accessTokenJwtType, KeepDefaultAccessTokenJwtType))
                {
                    o.AccessTokenJwtType = accessTokenJwtType!;
                }
            })
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddSpaces(spaces =>
            {
                spaces.FallbackToDefault = true;
                spaces.PartitionDataProtection = false;
            });

        if (sharedKey)
        {
            _ = identityServerBuilder.AddDeveloperSigningCredential(persistKey: false);
        }

        var scimUserProfileSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            DisplayName = "SCIM User Profile",
            Groups = [.. DefaultScimUserSchema.Groups],
            AttributeDefinitions = [.. DefaultScimUserSchema.AttributeDefinitions]
        };
        _ = identityServerBuilder.AddInMemoryDataExtensionSchemas([scimUserProfileSchema]);

        _ = identityServerBuilder.AddUserManagement(users => users.EnableScim(_ => { }));

        _ = builder.Services.AddLogging();

        var app = builder.Build();

        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        // Pipeline order: space resolution, then routing, then IdentityServer's own middleware, then
        // authorization, then the SCIM minimal-API endpoints. UseSpaceResolution() must come before
        // routing/authorization, because the path rewrite that lets /t/{space}/... match happens there.
        _ = app.UseSpaceResolution();
        _ = app.UseRouting();
        _ = app.UseIdentityServer();
        _ = app.UseAuthorization();
        _ = app.MapScim();

        // Test-only endpoint: mints two JWTs with identical claims using the current request's
        // space (issuer and signing key resolved per request, same as ScimLocalIssuerConfigurationManager
        // does), one typed as an access token and one typed as an identity token, so tests can check
        // that SCIM rejects the identity token on type alone.
        _ = app.MapGet("/test/mint-tokens", async (
            IIssuerNameService issuerNameService,
            IKeyMaterialService keyMaterialService,
            IdentityServerOptions identityServerOptions,
            Ct ct) =>
        {
            var issuer = await issuerNameService.GetCurrentAsync(ct);
            var credential = await keyMaterialService.GetSigningCredentialsAsync(null, ct);

            var now = DateTimeOffset.UtcNow;
            var payload = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["iss"] = issuer,
                ["aud"] = ScimResources.Audience,
                ["scope"] = ScimResources.FullAccessScopeName,
                ["client_id"] = "scim-full",
                ["iat"] = now.ToUnixTimeSeconds(),
                ["nbf"] = now.ToUnixTimeSeconds(),
                ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds()
            });

            var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };

            var accessTokenHeader = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(identityServerOptions.AccessTokenJwtType))
            {
                accessTokenHeader["typ"] = identityServerOptions.AccessTokenJwtType;
            }

            var accessToken = handler.CreateToken(payload, credential, accessTokenHeader);
            var idToken = handler.CreateToken(payload, credential, new Dictionary<string, object> { ["typ"] = "JWT" });

            return Microsoft.AspNetCore.Http.Results.Json(new { accessToken, idToken });
        });

        await app.StartAsync();
        return app;
    }

    private async Task<SpaceId> CreateSpaceAsync(WebApplication app, string name, string path)
    {
        var spaceAdmin = app.Services.GetRequiredService<ISpaceAdmin>();
        var result = await spaceAdmin.CreateAsync(
            new CreateSpaceConfiguration { Name = name, MatchPatterns = [new SpaceMatchPattern { Path = path }] },
            Ct);
        result.IsSuccess.ShouldBeTrue($"Create {name} failed: {result}");
        return result.Id!.Value;
    }

    /// <summary>
    /// Seeds the SCIM API resource/scopes and both SCIM clients (full-access and read-only) into
    /// <paramref name="spaceId"/>, using <paramref name="secretSuffix"/> to keep the same client IDs
    /// across spaces while giving each space a different secret.
    /// </summary>
    private async Task SeedScimClientsAsync(WebApplication app, SpaceId spaceId, string secretSuffix)
    {
        using var scope = app.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        using (accessor.SetSpace(spaceId))
        {
            var apiScopeAdmin = scope.ServiceProvider.GetRequiredService<IApiScopeAdmin>();
            var apiResourceAdmin = scope.ServiceProvider.GetRequiredService<IApiResourceAdmin>();
            var clientAdmin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();

            foreach (var scopeName in new[] { ScimResources.FullAccessScopeName, ScimResources.ReadOnlyScopeName })
            {
                var scopeResult = await apiScopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, Ct);
                scopeResult.IsSuccess.ShouldBeTrue($"[{spaceId}] create scope {scopeName} failed: {scopeResult}");
            }

            var resourceResult = await apiResourceAdmin.CreateAsync(new CreateApiResource
            {
                Name = ScimResources.Audience,
                Scopes = [ScimResources.FullAccessScopeName, ScimResources.ReadOnlyScopeName]
            }, Ct);
            resourceResult.IsSuccess.ShouldBeTrue($"[{spaceId}] create scim API resource failed: {resourceResult}");

            var fullResult = await clientAdmin.CreateAsync(new CreateClient
            {
                ClientId = "scim-full",
                RequireClientSecret = true,
                AllowedGrantTypes = ["client_credentials"],
                AllowedScopes = [ScimResources.FullAccessScopeName],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = $"full-{secretSuffix}" }]
            }, Ct);
            fullResult.IsSuccess.ShouldBeTrue($"[{spaceId}] create scim-full failed: {fullResult}");

            var readResult = await clientAdmin.CreateAsync(new CreateClient
            {
                ClientId = "scim-read",
                RequireClientSecret = true,
                AllowedGrantTypes = ["client_credentials"],
                AllowedScopes = [ScimResources.ReadOnlyScopeName],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = $"read-{secretSuffix}" }]
            }, Ct);
            readResult.IsSuccess.ShouldBeTrue($"[{spaceId}] create scim-read failed: {readResult}");
        }
    }

    private async Task<string> GetTokenAsync(HttpClient client, string tokenEndpointPrefix, string clientId, string secret, string scope)
    {
        using var response = await client.PostAsync($"{tokenEndpointPrefix}/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["scope"] = scope
        }), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, $"token request to {tokenEndpointPrefix}/connect/token failed: {body}");
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private static HttpClient NewClient(WebApplication app, string? token = null)
    {
        var client = app.GetTestClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private static StringContent NewUserContent(string userName) => new(
        JsonSerializer.Serialize(new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
            userName,
            emails = new[] { new { value = $"{userName}@example.com", primary = true } }
        }),
        System.Text.Encoding.UTF8,
        "application/scim+json");

    // Documentation example: registering SCIM resources with Spaces, and the pipeline order
    // UseSpaceResolution() needs relative to UseRouting()/UseAuthorization(). Exercised end to
    // end below, so the snippet stays correct.
    [Fact]
    public async Task SpacesHostSetup_registers_resources_and_orders_pipeline_for_docs()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        _ = TestUserManagementLicense.Create().Register(builder.Services, TimeProvider.System);

        // begin-snippet: scim-spaces-setup
        var identityServerBuilder = builder.Services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddSpaces(spaces => spaces.FallbackToDefault = true);

        _ = identityServerBuilder.AddUserManagement(users => users.EnableScim(_ => { }));
        // end-snippet

        var scimUserProfileSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            DisplayName = "SCIM User Profile",
            Groups = [.. DefaultScimUserSchema.Groups],
            AttributeDefinitions = [.. DefaultScimUserSchema.AttributeDefinitions]
        };
        _ = identityServerBuilder.AddInMemoryDataExtensionSchemas([scimUserProfileSchema]);

        _ = builder.Services.AddLogging();

        var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        // begin-snippet: scim-spaces-pipeline
        // UseSpaceResolution() must run before UseRouting() and UseAuthorization(): the path
        // rewrite that lets /t/{space}/... match happens there. Put it after routing (or leave it
        // out) and a space-prefixed request like /t/space-a/scim/Users never matches a route, so
        // it 404s before reaching SCIM authentication; no error is logged. The 401 with an error
        // naming UseSpaceResolution() happens when a request does reach SCIM authentication with
        // no space set, for example a default-space URL like /scim/Users, or origin-based spaces.
        _ = app.UseSpaceResolution();
        _ = app.UseRouting();
        _ = app.UseIdentityServer();
        _ = app.UseAuthorization();
        _ = app.MapScim();
        // end-snippet

        await app.StartAsync();
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        await SeedScimClientsAsync(app, spaceA, "a");

        using var tokenClient = NewClient(app);
        var token = await GetTokenAsync(tokenClient, "/t/space-a", "scim-full", "full-a", ScimResources.FullAccessScopeName);

        using var client = NewClient(app, token);
        using var response = await client.GetAsync("/t/space-a/scim/Users", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SpaceA_scimFull_can_read_and_write()
    {
        var app = await CreateSpacesScimServerAsync();
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        await SeedScimClientsAsync(app, spaceA, "a");

        using var tokenClient = NewClient(app);
        var token = await GetTokenAsync(tokenClient, "/t/space-a", "scim-full", "full-a", ScimResources.FullAccessScopeName);

        using var client = NewClient(app, token);
        using var listResponse = await client.GetAsync("/t/space-a/scim/Users", Ct);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var createResponse = await client.PostAsync("/t/space-a/scim/Users", NewUserContent("erin"), Ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task SpaceA_scimRead_can_read_but_not_write()
    {
        var app = await CreateSpacesScimServerAsync();
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        await SeedScimClientsAsync(app, spaceA, "a");

        using var tokenClient = NewClient(app);
        var token = await GetTokenAsync(tokenClient, "/t/space-a", "scim-read", "read-a", ScimResources.ReadOnlyScopeName);

        using var client = NewClient(app, token);
        using var listResponse = await client.GetAsync("/t/space-a/scim/Users", Ct);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var createResponse = await client.PostAsync("/t/space-a/scim/Users", NewUserContent("erin"), Ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// One row per (token-issuing space, target path) pair. Every cross-space combination among
    /// space-a, space-b and the default space is rejected with 401; a token is accepted only at
    /// the path for the space that issued it. Same assertions as the five single-case tests this
    /// theory replaces, just folded into one table.
    /// </summary>
    public static TheoryData<string, string, HttpStatusCode> TokenSpaceAndTargetPathCases => new()
    {
        { "space-a", "space-b", HttpStatusCode.Unauthorized },
        { "space-a", "default", HttpStatusCode.Unauthorized },
        { "space-b", "space-a", HttpStatusCode.Unauthorized },
        { "space-b", "default", HttpStatusCode.Unauthorized },
        { "default", "space-a", HttpStatusCode.Unauthorized },
        { "default", "space-b", HttpStatusCode.Unauthorized },
        { "space-b", "space-b", HttpStatusCode.OK },
        { "default", "default", HttpStatusCode.OK },
    };

    private static (string TokenEndpointPrefix, string TargetUrl) ResolveSpace(string space) => space switch
    {
        "space-a" => ("/t/space-a", "/t/space-a/scim/Users"),
        "space-b" => ("/t/space-b", "/t/space-b/scim/Users"),
        "default" => ("", "/scim/Users"),
        _ => throw new ArgumentOutOfRangeException(nameof(space), space, null)
    };

    private static string SecretSuffix(string space) => space switch
    {
        "space-a" => "a",
        "space-b" => "b",
        "default" => "default",
        _ => throw new ArgumentOutOfRangeException(nameof(space), space, null)
    };

    [Theory]
    [MemberData(nameof(TokenSpaceAndTargetPathCases))]
    public async Task Token_is_accepted_only_in_the_space_that_issued_it(string issuingSpace, string targetSpace, HttpStatusCode expected)
    {
        var app = await CreateSpacesScimServerAsync();
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        var spaceB = await CreateSpaceAsync(app, "Space B", SpaceBPath);
        await SeedScimClientsAsync(app, spaceA, "a");
        await SeedScimClientsAsync(app, spaceB, "b");
        await SeedScimClientsAsync(app, SpaceId.Default, "default");

        var (tokenEndpointPrefix, _) = ResolveSpace(issuingSpace);
        var (_, targetUrl) = ResolveSpace(targetSpace);

        using var tokenClient = NewClient(app);
        var token = await GetTokenAsync(tokenClient, tokenEndpointPrefix, "scim-full", $"full-{SecretSuffix(issuingSpace)}", ScimResources.FullAccessScopeName);

        using var client = NewClient(app, token);
        using var response = await client.GetAsync(targetUrl, Ct);
        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Same_host_alternating_spaces_each_accept_own_token_and_reject_others()
    {
        var app = await CreateSpacesScimServerAsync();
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        var spaceB = await CreateSpaceAsync(app, "Space B", SpaceBPath);
        await SeedScimClientsAsync(app, spaceA, "a");
        await SeedScimClientsAsync(app, spaceB, "b");
        await SeedScimClientsAsync(app, SpaceId.Default, "default");

        using var tokenClient = NewClient(app);
        var tokenA = await GetTokenAsync(tokenClient, "/t/space-a", "scim-full", "full-a", ScimResources.FullAccessScopeName);
        var tokenB = await GetTokenAsync(tokenClient, "/t/space-b", "scim-full", "full-b", ScimResources.FullAccessScopeName);
        var tokenDefault = await GetTokenAsync(tokenClient, "", "scim-full", "full-default", ScimResources.FullAccessScopeName);

        async Task AssertAsync(string token, string path, HttpStatusCode expected, string label)
        {
            using var client = NewClient(app, token);
            using var response = await client.GetAsync(path, Ct);
            response.StatusCode.ShouldBe(expected, label);
        }

        // Interleave own-space requests on the same host, so a manager that cached the first
        // request's issuer/keys (instead of resolving them per request from the current space)
        // would wrongly keep accepting that same issuer for every later space.
        await AssertAsync(tokenA, "/t/space-a/scim/Users", HttpStatusCode.OK, "space-a token @ own space (1st)");
        await AssertAsync(tokenB, "/t/space-b/scim/Users", HttpStatusCode.OK, "space-b token @ own space (1st)");
        await AssertAsync(tokenDefault, "/scim/Users", HttpStatusCode.OK, "default token @ own space (1st)");
        await AssertAsync(tokenA, "/t/space-a/scim/Users", HttpStatusCode.OK, "space-a token @ own space (2nd)");

        await AssertAsync(tokenA, "/t/space-b/scim/Users", HttpStatusCode.Unauthorized, "space-a token @ space-b");
        await AssertAsync(tokenB, "/scim/Users", HttpStatusCode.Unauthorized, "space-b token @ default");
        await AssertAsync(tokenDefault, "/t/space-a/scim/Users", HttpStatusCode.Unauthorized, "default token @ space-a");

        await AssertAsync(tokenB, "/t/space-b/scim/Users", HttpStatusCode.OK, "space-b token @ own space (2nd)");
    }

    // Shared-key variant: KeyManagement.Enabled = false + a single developer signing credential.
    // Cross-space tokens must still be rejected, because the issuer alone (not the key) separates
    // the spaces.
    [Fact]
    public async Task SharedKey_cross_space_token_still_rejected_by_issuer_alone()
    {
        var app = await CreateSpacesScimServerAsync(sharedKey: true);
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        var spaceB = await CreateSpaceAsync(app, "Space B", SpaceBPath);
        await SeedScimClientsAsync(app, spaceA, "a");
        await SeedScimClientsAsync(app, spaceB, "b");

        using var tokenClient = NewClient(app);
        var tokenA = await GetTokenAsync(tokenClient, "/t/space-a", "scim-full", "full-a", ScimResources.FullAccessScopeName);

        using var client = NewClient(app, tokenA);
        using var atSpaceB = await client.GetAsync("/t/space-b/scim/Users", Ct);
        atSpaceB.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // Without Spaces: no Authority, no IssuerUri -> SCIM accepts IdentityServer's own tokens.

    private async Task<WebApplication> CreateNoSpacesScimServerAsync(string? issuerUri, bool backchannel)
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();

        _ = TestUserManagementLicense.Create().Register(builder.Services, TimeProvider.System);

        var identityServerBuilder = builder.Services.AddIdentityServer(o =>
            {
                if (issuerUri is not null)
                {
                    o.IssuerUri = issuerUri;
                }
            })
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddInMemoryClients([
                new Duende.IdentityServer.Models.Client
                {
                    ClientId = "scim-full",
                    AllowedGrantTypes = Duende.IdentityServer.Models.GrantTypes.ClientCredentials,
                    ClientSecrets = [new Duende.IdentityServer.Models.Secret("secret".Sha256())],
                    AllowedScopes = { ScimResources.FullAccessScopeName }
                }
            ])
            .AddInMemoryApiResources([ScimResources.ApiResource])
            .AddInMemoryApiScopes([ScimResources.FullAccessScope, ScimResources.ReadOnlyScope]);

        var scimUserProfileSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            DisplayName = "SCIM User Profile",
            Groups = [.. DefaultScimUserSchema.Groups],
            AttributeDefinitions = [.. DefaultScimUserSchema.AttributeDefinitions]
        };
        _ = identityServerBuilder.AddInMemoryDataExtensionSchemas([scimUserProfileSchema]);

        _ = identityServerBuilder.AddUserManagement(users => users.EnableScim(_ => { }));

        if (backchannel)
        {
            _ = builder.Services.AddOptions<JwtBearerOptions>("DuendeScimBearer")
                .Configure<IServer>((o, server) => o.BackchannelHttpHandler = ((TestServer)server).CreateHandler());
        }

        _ = builder.Services.AddLogging();

        var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        _ = app.UseRouting();
        _ = app.UseIdentityServer();
        _ = app.UseAuthorization();
        _ = app.MapScim();

        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Without_Spaces_no_Authority_no_IssuerUri_accepts_own_tokens()
    {
        var app = await CreateNoSpacesScimServerAsync(issuerUri: null, backchannel: true);
        await using var disposable = app;

        using var tokenClient = NewClient(app);
        var token = await GetTokenAsync(tokenClient, "", "scim-full", "secret", ScimResources.FullAccessScopeName);

        using var client = NewClient(app, token);
        using var response = await client.GetAsync("/scim/Users", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IssuerUri_set_no_Authority_accepts_own_tokens()
    {
        // No BackchannelHttpHandler configured here: this proves there is no HTTP self-call when
        // IssuerUri is set but Authority is not.
        var app = await CreateNoSpacesScimServerAsync(issuerUri: "https://idp.example.com", backchannel: false);
        await using var disposable = app;

        using var tokenClient = NewClient(app);
        var token = await GetTokenAsync(tokenClient, "", "scim-full", "secret", ScimResources.FullAccessScopeName);

        using var client = NewClient(app, token);
        using var response = await client.GetAsync("/scim/Users", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Explicit Authority facts.

    [Fact]
    public void Explicit_Authority_with_Spaces_and_builtin_policies_fails_at_startup()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = TestUserManagementLicense.Create().Register(services, TimeProvider.System);

        _ = services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddSpaces(spaces =>
            {
                spaces.FallbackToDefault = true;
                spaces.PartitionDataProtection = false;
            })
            .AddUserManagement(users =>
            {
                _ = users.EnableScim(_ => { });
                _ = users.ConfigureScimOAuth(o => o.Authority = "http://localhost");
            });

        using var sp = services.BuildServiceProvider();

        var ex = Should.Throw<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<ScimOAuthOptions>>().Value);

        Output.WriteLine($"OptionsValidationException: {ex.Message}");

        // The message must point to both ways out.
        ex.Message.ShouldContain("Authority");
        ex.Message.ShouldContain("ReadAuthorizationPolicyName");
        ex.Message.ShouldContain("WriteAuthorizationPolicyName");
    }

    [Fact]
    public async Task Explicit_Authority_without_Spaces_works()
    {
        const string RootUrl = "http://localhost";

        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();

        _ = TestUserManagementLicense.Create().Register(builder.Services, TimeProvider.System);

        var identityServerBuilder = builder.Services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddInMemoryClients([
                new Duende.IdentityServer.Models.Client
                {
                    ClientId = "scim-full",
                    AllowedGrantTypes = Duende.IdentityServer.Models.GrantTypes.ClientCredentials,
                    ClientSecrets = [new Duende.IdentityServer.Models.Secret("secret".Sha256())],
                    AllowedScopes = { ScimResources.FullAccessScopeName }
                }
            ])
            .AddInMemoryApiResources([ScimResources.ApiResource])
            .AddInMemoryApiScopes([ScimResources.FullAccessScope, ScimResources.ReadOnlyScope]);

        var scimUserProfileSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            DisplayName = "SCIM User Profile",
            Groups = [.. DefaultScimUserSchema.Groups],
            AttributeDefinitions = [.. DefaultScimUserSchema.AttributeDefinitions]
        };
        _ = identityServerBuilder.AddInMemoryDataExtensionSchemas([scimUserProfileSchema]);

        _ = identityServerBuilder.AddUserManagement(users =>
        {
            _ = users.EnableScim(_ => { });
            _ = users.ConfigureScimOAuth(o =>
            {
                o.Authority = RootUrl;
                o.RequireHttpsMetadata = false;
            });
        });

        // The SCIM JwtBearer handler's discovery fetch must loop back into this same in-memory
        // TestServer. No Configure<JwtBearerOptions>(... Authority ...) workaround is applied here:
        // an explicit Authority alone must be enough.
        _ = builder.Services.AddOptions<JwtBearerOptions>("DuendeScimBearer")
            .Configure<IServer>((o, server) => o.BackchannelHttpHandler = ((TestServer)server).CreateHandler());

        var authFailures = new List<string>();
        _ = builder.Services.Configure<JwtBearerOptions>("DuendeScimBearer", o =>
        {
            o.Events ??= new JwtBearerEvents();
            var inner = o.Events.OnAuthenticationFailed;
            o.Events.OnAuthenticationFailed = ctx =>
            {
                lock (authFailures)
                {
                    authFailures.Add(ctx.Exception.Message);
                }

                return inner?.Invoke(ctx) ?? Task.CompletedTask;
            };
        });

        _ = builder.Services.AddLogging();

        var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        _ = app.UseRouting();
        _ = app.UseIdentityServer();
        _ = app.UseAuthorization();
        _ = app.MapScim();

        await app.StartAsync();
        await using var disposable = app;

        using var tokenClient = NewClient(app);
        var token = await GetTokenAsync(tokenClient, "", "scim-full", "secret", ScimResources.FullAccessScopeName);

        using var client = NewClient(app, token);
        using var readResponse = await client.GetAsync("/scim/Users", Ct);
        lock (authFailures)
        {
            Output.WriteLine($"captured auth-failure log lines: {string.Join(" || ", authFailures)}");
        }

        readResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var createResponse = await client.PostAsync("/scim/Users", NewUserContent("erin"), Ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // A host that keeps the pre-fix Configure<JwtBearerOptions>("DuendeScimBearer", o =>
    // o.Authority = ...) workaround, with ScimOAuthOptions.Authority left unset, still starts and
    // accepts its own scim tokens. IssuerUri is no longer copied into ScimOAuthOptions.Authority,
    // so the in-process local-issuer manager is the expected path: the workaround's
    // Configure call only sets JwtBearerOptions.Authority, never ConfigurationManager, so
    // ScimLocalIssuerJwtBearerConfigureOptions is the only configure step that ever assigns
    // ConfigurationManager here, and it does so whenever ScimOAuthOptions.Authority is blank,
    // regardless of the Configure calls' registration order.

    [Fact]
    public async Task Host_keeping_the_old_Authority_workaround_still_starts_and_accepts_its_own_tokens()
    {
        const string RootUrl = "http://localhost";

        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();

        _ = TestUserManagementLicense.Create().Register(builder.Services, TimeProvider.System);

        var identityServerBuilder = builder.Services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddInMemoryClients([
                new Duende.IdentityServer.Models.Client
                {
                    ClientId = "scim-full",
                    AllowedGrantTypes = Duende.IdentityServer.Models.GrantTypes.ClientCredentials,
                    ClientSecrets = [new Duende.IdentityServer.Models.Secret("secret".Sha256())],
                    AllowedScopes = { ScimResources.FullAccessScopeName }
                }
            ])
            .AddInMemoryApiResources([ScimResources.ApiResource])
            .AddInMemoryApiScopes([ScimResources.FullAccessScope, ScimResources.ReadOnlyScope]);

        var scimUserProfileSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            DisplayName = "SCIM User Profile",
            Groups = [.. DefaultScimUserSchema.Groups],
            AttributeDefinitions = [.. DefaultScimUserSchema.AttributeDefinitions]
        };
        _ = identityServerBuilder.AddInMemoryDataExtensionSchemas([scimUserProfileSchema]);

        // No ConfigureScimOAuth call at all: ScimOAuthOptions.Authority stays unset.
        _ = identityServerBuilder.AddUserManagement(users => users.EnableScim(_ => { }));

        // The pre-fix workaround from docs/enable-scim.md, kept by a host that hasn't removed it
        // yet. Also keeps the old backchannel/https relaxations. The workaround's Configure call
        // only sets JwtBearerOptions.Authority, never ConfigurationManager, so it doesn't block
        // ScimLocalIssuerJwtBearerConfigureOptions from setting ConfigurationManager below.
        _ = builder.Services.Configure<JwtBearerOptions>("DuendeScimBearer", o => o.Authority = RootUrl);
        _ = builder.Services.Configure<JwtBearerOptions>("DuendeScimBearer", o => o.RequireHttpsMetadata = false);
        _ = builder.Services.AddOptions<JwtBearerOptions>("DuendeScimBearer")
            .Configure<IServer>((o, server) => o.BackchannelHttpHandler = ((TestServer)server).CreateHandler());

        _ = builder.Services.AddLogging();

        var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        _ = app.UseRouting();
        _ = app.UseIdentityServer();
        _ = app.UseAuthorization();
        _ = app.MapScim();

        await app.StartAsync();
        await using var disposable = app;

        // The workaround's Configure call only sets JwtBearerOptions.Authority, never
        // ConfigurationManager, so it never stops ScimLocalIssuerJwtBearerConfigureOptions
        // (run through EnableScim) from assigning the in-process local-issuer manager:
        // that step only skips when ScimOAuthOptions.Authority is set or ConfigurationManager
        // is already non-null, neither of which happens here. Assert that guarantee.
        var jwtBearerOptions = app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("DuendeScimBearer");
        Output.WriteLine(
            $"D4: JwtBearerOptions.Authority='{jwtBearerOptions.Authority}', " +
            $"ConfigurationManager={jwtBearerOptions.ConfigurationManager?.GetType().Name ?? "(null)"}");
        _ = jwtBearerOptions.ConfigurationManager.ShouldBeOfType<ScimLocalIssuerConfigurationManager>();

        using var tokenClient = NewClient(app);
        var token = await GetTokenAsync(tokenClient, "", "scim-full", "secret", ScimResources.FullAccessScopeName);

        using var client = NewClient(app, token);
        using var response = await client.GetAsync("/scim/Users", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Spaces registered, but no space set for the request (e.g. UseSpaceResolution() missing
    // or out of order) -> fail closed with 401, and log an error naming UseSpaceResolution().

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner.Entries)
                {
                    owner.Entries.Add((logLevel, formatter(state, exception)));
                }
            }
        }
    }

    [Fact]
    public async Task No_space_set_fails_closed_with_401_and_logs_error_naming_UseSpaceResolution()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();

        _ = TestUserManagementLicense.Create().Register(builder.Services, TimeProvider.System);

        var identityServerBuilder = builder.Services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddSpaces(spaces =>
            {
                spaces.FallbackToDefault = true;
                spaces.PartitionDataProtection = false;
            });

        var scimUserProfileSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            DisplayName = "SCIM User Profile",
            Groups = [.. DefaultScimUserSchema.Groups],
            AttributeDefinitions = [.. DefaultScimUserSchema.AttributeDefinitions]
        };
        _ = identityServerBuilder.AddInMemoryDataExtensionSchemas([scimUserProfileSchema]);

        _ = identityServerBuilder.AddUserManagement(users =>
        {
            users.EnableScim(_ => { });
        });

        var capturingProvider = new CapturingLoggerProvider();
        _ = builder.Services.AddLogging(lb => lb.AddProvider(capturingProvider));

        var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        // Seed a default-space token in a scope where the space context is set, so we have a
        // validly-issued token to send. The pipeline below deliberately does NOT call
        // UseSpaceResolution(), so no space is ever set for incoming requests, and the SCIM
        // in-process configuration manager must fail closed.
        await SeedScimClientsAsync(app, SpaceId.Default, "default");

        // Pipeline order deliberately omits UseSpaceResolution(): routing/auth/SCIM run with no
        // space set for the request, which is the misconfiguration this test covers.
        _ = app.UseRouting();
        _ = app.UseIdentityServer();
        _ = app.UseAuthorization();
        _ = app.MapScim();

        await app.StartAsync();
        await using var disposable = app;

        using var tokenClient = NewClient(app);
        var token = await GetTokenAsync(tokenClient, "", "scim-full", "full-default", ScimResources.FullAccessScopeName);

        using var client = NewClient(app, token);
        using var response = await client.GetAsync("/scim/Users", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        lock (capturingProvider.Entries)
        {
            Output.WriteLine($"captured log entries: {string.Join(" || ", capturingProvider.Entries.Select(e => $"[{e.Level}] {e.Message}"))}");
            capturingProvider.Entries.ShouldContain(e =>
                e.Level == LogLevel.Error &&
                e.Message.Contains("UseSpaceResolution", StringComparison.Ordinal));
        }
    }

    // One fixed Authority with Spaces, limiting each space to its own client through host
    // read and write policies built on ReadAuthorizationPolicyName/WriteAuthorizationPolicyName.

    // begin-snippet: scim-client-per-space-policy
    /// <summary>
    /// Authorization requirement that limits a SCIM policy to the one client allowed in the
    /// current space. A space with no entry in <see cref="ClientIdsBySpace"/> (including the
    /// default space) always fails.
    /// </summary>
    private sealed class ScimClientForSpaceRequirement(IReadOnlyDictionary<SpaceId, string> clientIdsBySpace)
        : IAuthorizationRequirement
    {
        public IReadOnlyDictionary<SpaceId, string> ClientIdsBySpace => clientIdsBySpace;
    }

    private sealed class ScimClientForSpaceHandler(ISpaceContextAccessor spaces)
        : AuthorizationHandler<ScimClientForSpaceRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ScimClientForSpaceRequirement requirement)
        {
            if (!spaces.IsSpaceIdConfigured())
            {
                return Task.CompletedTask;
            }

            var spaceId = spaces.GetSpaceId();
            var clientId = context.User.FindFirst("client_id")?.Value;

            if (requirement.ClientIdsBySpace.TryGetValue(spaceId, out var allowedClientId) &&
                string.Equals(clientId, allowedClientId, StringComparison.Ordinal))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }

    // IdentityServer can issue the "scope" claim either as one claim per scope (the default) or
    // as a single space-delimited claim (IdentityServerOptions.EmitScopesAsSpaceDelimitedStringInJwt
    // = true). RequireClaim only matches a claim whose whole value equals one of the given values,
    // so it misses the space-delimited form. This helper collects scopes from every "scope" claim,
    // splitting on spaces, and works for both formats.
    private static bool HasAnyScimScope(AuthorizationHandlerContext context, params string[] requiredScopes)
    {
        foreach (var claim in context.User.FindAll("scope"))
        {
            foreach (var scope in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Array.Exists(requiredScopes, required => string.Equals(scope, required, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AddScimClientPerSpacePolicies(IServiceCollection services, IReadOnlyDictionary<SpaceId, string> clientIdsBySpace)
    {
        _ = services.AddSingleton<IAuthorizationHandler>(sp =>
            new ScimClientForSpaceHandler(sp.GetRequiredService<ISpaceContextAccessor>()));

        _ = services.AddAuthorizationBuilder()
            .AddPolicy("ScimRead", policy => policy
                .AddAuthenticationSchemes("DuendeScimBearer")
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasAnyScimScope(context, "scim", "scim.read"))
                .AddRequirements(new ScimClientForSpaceRequirement(clientIdsBySpace)))
            .AddPolicy("ScimWrite", policy => policy
                .AddAuthenticationSchemes("DuendeScimBearer")
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasAnyScimScope(context, "scim"))
                .AddRequirements(new ScimClientForSpaceRequirement(clientIdsBySpace)));
    }
    // end-snippet

    private async Task<WebApplication> CreateSpacesScimServerWithClientPerSpacePolicyAsync(
        IReadOnlyDictionary<SpaceId, string> clientIdsBySpace,
        bool emitScopesAsSpaceDelimitedStringInJwt = false)
    {
        const string RootUrl = "http://localhost";

        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();

        _ = TestUserManagementLicense.Create().Register(builder.Services, TimeProvider.System);

        var identityServerBuilder = builder.Services.AddIdentityServer(options =>
            {
                options.EmitScopesAsSpaceDelimitedStringInJwt = emitScopesAsSpaceDelimitedStringInJwt;
            })
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddSpaces(spaces =>
            {
                spaces.FallbackToDefault = true;
                spaces.PartitionDataProtection = false;
            });

        var scimUserProfileSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            DisplayName = "SCIM User Profile",
            Groups = [.. DefaultScimUserSchema.Groups],
            AttributeDefinitions = [.. DefaultScimUserSchema.AttributeDefinitions]
        };
        _ = identityServerBuilder.AddInMemoryDataExtensionSchemas([scimUserProfileSchema]);

        _ = identityServerBuilder.AddUserManagement(users =>
        {
            _ = users.EnableScim(_ => { });
            _ = users.ConfigureScimOAuth(o =>
            {
                o.Authority = RootUrl;
                o.RequireHttpsMetadata = false;
                o.ReadAuthorizationPolicyName = "ScimRead";
                o.WriteAuthorizationPolicyName = "ScimWrite";
            });
        });

        AddScimClientPerSpacePolicies(builder.Services, clientIdsBySpace);

        _ = builder.Services.AddOptions<JwtBearerOptions>(ScimConstants.AuthenticationScheme)
            .Configure<IServer>((o, server) => o.BackchannelHttpHandler = ((TestServer)server).CreateHandler());

        _ = builder.Services.AddLogging();

        var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        _ = app.UseSpaceResolution();
        _ = app.UseRouting();
        _ = app.UseIdentityServer();
        _ = app.UseAuthorization();
        _ = app.MapScim();

        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Single_authority_with_client_per_space_policy()
    {
        var clientIdsBySpace = new Dictionary<SpaceId, string>();

        var app = await CreateSpacesScimServerWithClientPerSpacePolicyAsync(clientIdsBySpace);
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        var spaceB = await CreateSpaceAsync(app, "Space B", SpaceBPath);
        clientIdsBySpace[spaceA] = "acme-scim";
        clientIdsBySpace[spaceB] = "globex-scim";

        // Clients live in the default space, since Authority is the default space's (root) issuer.
        using (var scope = app.Services.CreateScope())
        {
            var clientAdmin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();
            var apiScopeAdmin = scope.ServiceProvider.GetRequiredService<IApiScopeAdmin>();
            var apiResourceAdmin = scope.ServiceProvider.GetRequiredService<IApiResourceAdmin>();

            foreach (var scopeName in new[] { ScimResources.FullAccessScopeName, ScimResources.ReadOnlyScopeName })
            {
                var scopeResult = await apiScopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, Ct);
                scopeResult.IsSuccess.ShouldBeTrue($"create scope {scopeName} failed: {scopeResult}");
            }

            var resourceResult = await apiResourceAdmin.CreateAsync(new CreateApiResource
            {
                Name = ScimResources.Audience,
                Scopes = [ScimResources.FullAccessScopeName, ScimResources.ReadOnlyScopeName]
            }, Ct);
            resourceResult.IsSuccess.ShouldBeTrue($"create scim API resource failed: {resourceResult}");

            var acmeResult = await clientAdmin.CreateAsync(new CreateClient
            {
                ClientId = "acme-scim",
                RequireClientSecret = true,
                AllowedGrantTypes = ["client_credentials"],
                AllowedScopes = [ScimResources.FullAccessScopeName, ScimResources.ReadOnlyScopeName],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = "acme-secret" }]
            }, Ct);
            acmeResult.IsSuccess.ShouldBeTrue($"create acme-scim failed: {acmeResult}");

            var globexResult = await clientAdmin.CreateAsync(new CreateClient
            {
                ClientId = "globex-scim",
                RequireClientSecret = true,
                AllowedGrantTypes = ["client_credentials"],
                AllowedScopes = [ScimResources.FullAccessScopeName],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = "globex-secret" }]
            }, Ct);
            globexResult.IsSuccess.ShouldBeTrue($"create globex-scim failed: {globexResult}");
        }

        using var tokenClient = NewClient(app);
        // acme-scim requests two scopes (scim and scim.read) in one token, to prove that
        // RequireClaim("scope", "scim", "scim.read") still matches when IdentityServer emits a
        // "scope" claim per scope (MapInboundClaims = false) rather than one space-delimited value.
        var acmeToken = await GetTokenAsync(
            tokenClient, "", "acme-scim", "acme-secret",
            $"{ScimResources.FullAccessScopeName} {ScimResources.ReadOnlyScopeName}");
        var globexToken = await GetTokenAsync(tokenClient, "", "globex-scim", "globex-secret", ScimResources.FullAccessScopeName);

        // acme-scim: allowed only in space-a.
        using (var acmeClient = NewClient(app, acmeToken))
        {
            using var getA = await acmeClient.GetAsync("/t/space-a/scim/Users", Ct);
            getA.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var postA = await acmeClient.PostAsync("/t/space-a/scim/Users", NewUserContent("acme-user"), Ct);
            postA.StatusCode.ShouldBe(HttpStatusCode.Created);

            using var getB = await acmeClient.GetAsync("/t/space-b/scim/Users", Ct);
            getB.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

            using var getDefault = await acmeClient.GetAsync("/scim/Users", Ct);
            getDefault.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // globex-scim: allowed only in space-b.
        using (var globexClient = NewClient(app, globexToken))
        {
            using var getB = await globexClient.GetAsync("/t/space-b/scim/Users", Ct);
            getB.StatusCode.ShouldBe(HttpStatusCode.OK);

            using var postB = await globexClient.PostAsync("/t/space-b/scim/Users", NewUserContent("globex-user"), Ct);
            postB.StatusCode.ShouldBe(HttpStatusCode.Created);

            using var getA = await globexClient.GetAsync("/t/space-a/scim/Users", Ct);
            getA.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

            using var getDefault = await globexClient.GetAsync("/scim/Users", Ct);
            getDefault.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Single_authority_with_client_per_space_policy_and_space_delimited_scopes()
    {
        var clientIdsBySpace = new Dictionary<SpaceId, string>();

        var app = await CreateSpacesScimServerWithClientPerSpacePolicyAsync(
            clientIdsBySpace, emitScopesAsSpaceDelimitedStringInJwt: true);
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        var spaceB = await CreateSpaceAsync(app, "Space B", SpaceBPath);
        clientIdsBySpace[spaceA] = "acme-scim";
        clientIdsBySpace[spaceB] = "globex-scim";

        // Clients live in the default space, since Authority is the default space's (root) issuer.
        using (var scope = app.Services.CreateScope())
        {
            var clientAdmin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();
            var apiScopeAdmin = scope.ServiceProvider.GetRequiredService<IApiScopeAdmin>();
            var apiResourceAdmin = scope.ServiceProvider.GetRequiredService<IApiResourceAdmin>();

            foreach (var scopeName in new[] { ScimResources.FullAccessScopeName, ScimResources.ReadOnlyScopeName })
            {
                var scopeResult = await apiScopeAdmin.CreateAsync(new CreateApiScope { Name = scopeName }, Ct);
                scopeResult.IsSuccess.ShouldBeTrue($"create scope {scopeName} failed: {scopeResult}");
            }

            var resourceResult = await apiResourceAdmin.CreateAsync(new CreateApiResource
            {
                Name = ScimResources.Audience,
                Scopes = [ScimResources.FullAccessScopeName, ScimResources.ReadOnlyScopeName]
            }, Ct);
            resourceResult.IsSuccess.ShouldBeTrue($"create scim API resource failed: {resourceResult}");

            var acmeResult = await clientAdmin.CreateAsync(new CreateClient
            {
                ClientId = "acme-scim",
                RequireClientSecret = true,
                AllowedGrantTypes = ["client_credentials"],
                AllowedScopes = [ScimResources.FullAccessScopeName, ScimResources.ReadOnlyScopeName],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = "acme-secret" }]
            }, Ct);
            acmeResult.IsSuccess.ShouldBeTrue($"create acme-scim failed: {acmeResult}");
        }

        using var tokenClient = NewClient(app);
        // With EmitScopesAsSpaceDelimitedStringInJwt = true, IdentityServer issues a single "scope"
        // claim whose value is "scim scim.read", to prove the policy matches that format too.
        var acmeToken = await GetTokenAsync(
            tokenClient, "", "acme-scim", "acme-secret",
            $"{ScimResources.FullAccessScopeName} {ScimResources.ReadOnlyScopeName}");

        // acme-scim: allowed only in space-a.
        using var acmeClient = NewClient(app, acmeToken);

        using var getA = await acmeClient.GetAsync("/t/space-a/scim/Users", Ct);
        getA.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var postA = await acmeClient.PostAsync("/t/space-a/scim/Users", NewUserContent("acme-user"), Ct);
        postA.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var getB = await acmeClient.GetAsync("/t/space-b/scim/Users", Ct);
        getB.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var getDefault = await acmeClient.GetAsync("/scim/Users", Ct);
        getDefault.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private sealed record MintedTokens(string AccessToken, string IdToken);

    private static async Task<MintedTokens> MintAccessAndIdTokensAsync(HttpClient client, string pathPrefix)
    {
        using var response = await client.GetAsync($"{pathPrefix}/test/mint-tokens", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return new MintedTokens(
            json.RootElement.GetProperty("accessToken").GetString()!,
            json.RootElement.GetProperty("idToken").GetString()!);
    }

    [Fact]
    public async Task InProcess_validation_rejects_an_identity_token_with_the_scim_audience_by_type()
    {
        var app = await CreateSpacesScimServerAsync();
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        await SeedScimClientsAsync(app, spaceA, "a");

        using var mintingClient = NewClient(app);
        var tokens = await MintAccessAndIdTokensAsync(mintingClient, "/t/space-a");

        using var accessClient = NewClient(app, tokens.AccessToken);
        using var accessResponse = await accessClient.GetAsync("/t/space-a/scim/Users", Ct);
        accessResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var idClient = NewClient(app, tokens.IdToken);
        using var idResponse = await idClient.GetAsync("/t/space-a/scim/Users", Ct);
        idResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InProcess_validation_with_a_custom_AccessTokenJwtType_still_accepts_its_own_access_tokens()
    {
        var app = await CreateSpacesScimServerAsync(accessTokenJwtType: "custom+jwt");
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        await SeedScimClientsAsync(app, spaceA, "a");

        using var mintingClient = NewClient(app);
        var tokens = await MintAccessAndIdTokensAsync(mintingClient, "/t/space-a");

        using var accessClient = NewClient(app, tokens.AccessToken);
        using var accessResponse = await accessClient.GetAsync("/t/space-a/scim/Users", Ct);
        accessResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InProcess_validation_with_no_AccessTokenJwtType_still_accepts_its_own_access_tokens()
    {
        var app = await CreateSpacesScimServerAsync(accessTokenJwtType: null);
        await using var disposable = app;

        var spaceA = await CreateSpaceAsync(app, "Space A", SpaceAPath);
        await SeedScimClientsAsync(app, spaceA, "a");

        using var mintingClient = NewClient(app);
        var tokens = await MintAccessAndIdTokensAsync(mintingClient, "/t/space-a");

        using var accessClient = NewClient(app, tokens.AccessToken);
        using var accessResponse = await accessClient.GetAsync("/t/space-a/scim/Users", Ct);
        accessResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
