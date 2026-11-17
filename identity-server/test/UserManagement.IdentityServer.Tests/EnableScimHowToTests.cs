// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UserManagement;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Scim;
using Duende.UserManagement.TestIsolation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityServer8.UnitTests;

/// <summary>
/// How-to tests for enabling SCIM provisioning. SCIM data endpoints require a license
/// entitled to the Inbound SCIM feature; <c>UserManagement.TestInfrastructure</c>'s test license covers it.
/// </summary>
public sealed class EnableScimHowToTests(WebServerFixture testServer)
{
    private static Ct Ct => TestContext.Current.CancellationToken;

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithScim(WebApplicationBuilder builder)
    {
        // begin-snippet: scim-enable
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.EnableScim(options =>
            {
                options.MaxResults = 50;
                options.MaxGroupMembersInResponse = 50;
                options.MaxBulkOperations = 20;
                options.MaxBulkPayloadSize = 256_000;
                options.ChangePassword = false;
            }));
        // end-snippet
    }
#pragma warning restore IDE0022

    private static void ConfigureDefaultScim(IUserManagementBuilder users) =>
        users.EnableScim(options =>
        {
            options.MaxResults = 50;
            options.MaxGroupMembersInResponse = 50;
            options.MaxBulkOperations = 20;
            options.MaxBulkPayloadSize = 256_000;
            options.ChangePassword = false;
        });

    [Fact]
    public async Task RegisterWithScim_snippet_enables_SCIM_with_configured_limits()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        _ = builder.Services.Configure<Duende.IdentityServer.Configuration.IdentityServerOptions>(
            o => o.IssuerUri = "https://idp.example.com");
        RegisterWithScim(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var options = app.Services.GetRequiredService<IOptions<ScimOptions>>().Value;
        options.MaxResults.ShouldBe(50);
        options.MaxGroupMembersInResponse.ShouldBe(50);
        options.MaxBulkOperations.ShouldBe(20);
        options.MaxBulkPayloadSize.ShouldBe(256_000);
        options.ChangePassword.ShouldBe(false);

        _ = app.MapScim(); // Throws if EnableScim wasn't actually registered.
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void ConfigureIdentityServerResources(IIdentityServerBuilder identityServer)
    {
        // begin-snippet: scim-identityserver-resources
        _ = identityServer
            .AddInMemoryApiResources([ScimResources.ApiResource])
            .AddInMemoryApiScopes([ScimResources.FullAccessScope, ScimResources.ReadOnlyScope]);
        // end-snippet
    }
#pragma warning restore IDE0022

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void MapScimEndpoints(WebAppWrapper app)
    {
        // begin-snippet: scim-map
        _ = app.MapScim();
        // end-snippet
    }
#pragma warning restore IDE0022

    private const string CustomPolicyName = "ScimCustomPolicy";
    private const string CustomSchemeName = "ScimApiKey";
    private const string ValidApiKey = "test-api-key-value";

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithCustomPolicy(WebApplicationBuilder builder)
    {
        // begin-snippet: scim-custom-policy
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users =>
            {
                _ = users.EnableScim(_ => { });
                _ = users.ConfigureScimOAuth(o => o.AuthorizationPolicyName = "ScimCustomPolicy");
            });
        // end-snippet
    }
#pragma warning restore IDE0022

    // The host registers the custom policy's own authentication scheme and the policy itself. Here the
    // scheme is a minimal test API-key handler; a real host would typically use AddJwtBearer against another
    // authority instead.
#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterCustomPolicyHost(WebApplicationBuilder builder)
    {
        // begin-snippet: scim-custom-policy-host
        _ = builder.Services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, TestApiKeyHandler>(CustomSchemeName, _ => { });
        _ = builder.Services.AddAuthorizationBuilder()
            .AddPolicy("ScimCustomPolicy", policy => policy
                .AddAuthenticationSchemes(CustomSchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "scim"));
        // end-snippet
    }
#pragma warning restore IDE0022

    private static void ConfigureCustomPolicy(IUserManagementBuilder users)
    {
        _ = users.EnableScim(_ => { });
        _ = users.ConfigureScimOAuth(o => o.AuthorizationPolicyName = CustomPolicyName);
    }

    private static void RegisterCustomPolicy(IServiceCollection services)
    {
        _ = services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, TestApiKeyHandler>(CustomSchemeName, _ => { });
        _ = services.AddAuthorizationBuilder()
            .AddPolicy(CustomPolicyName, policy => policy
                .AddAuthenticationSchemes(CustomSchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "scim"));
    }

    [Fact]
    public async Task RegisterWithCustomPolicy_snippets_enable_SCIM_behind_the_hosts_own_policy()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterCustomPolicyHost(builder);
        RegisterWithCustomPolicy(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var scimOAuthOptions = app.Services.GetRequiredService<IOptions<ScimOAuthOptions>>().Value;
        scimOAuthOptions.AuthorizationPolicyName.ShouldBe("ScimCustomPolicy");

        _ = app.MapScim(); // Throws if EnableScim wasn't actually registered.
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static IIdentityServerBuilder RegisterScimUserSchema(IIdentityServerBuilder identityServer)
    {
        // begin-snippet: scim-register-user-schema
        // AddUserManagement's default schema has no userName, which SCIM requires: every User resource is
        // keyed on it. Replace the profile schema with DefaultScimUserSchema (or extend your own schema with
        // its attributes) before enabling SCIM, so provisioned users get a userName to provision with.
        var scimUserProfileSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            DisplayName = "SCIM User Profile",
            Groups = [.. DefaultScimUserSchema.Groups],
            AttributeDefinitions = [.. DefaultScimUserSchema.AttributeDefinitions]
        };

        return identityServer.AddInMemoryDataExtensionSchemas([scimUserProfileSchema]);
        // end-snippet
    }
#pragma warning restore IDE0022

    private KestrelBasedTestServer CreateDefaultScimServer()
    {
        var server = new KestrelBasedTestServer(
            serverName: "scim-" + Guid.NewGuid().ToString("N"),
            fixture: testServer,
            output: TestContext.Current.TestOutputHelper!,
            configureServices: services =>
            {
                var identityServerBuilder = RegisterScimUserSchema(
                        services.AddIdentityServer()
                            .AddStorage(s => s.AddSqliteInMemory())
                            .AddConfigurationStorage()
                            .AddOperationalStorage())
                    .AddUserManagement(ConfigureDefaultScim)
                    .AddInMemoryClients([
                        new Client
                        {
                            ClientId = "scim-full-access",
                            AllowedGrantTypes = GrantTypes.ClientCredentials,
                            ClientSecrets = [new Secret("secret".Sha256())],
                            AllowedScopes = { ScimResources.FullAccessScopeName }
                        },
                        new Client
                        {
                            ClientId = "scim-read-only",
                            AllowedGrantTypes = GrantTypes.ClientCredentials,
                            ClientSecrets = [new Secret("secret".Sha256())],
                            AllowedScopes = { ScimResources.ReadOnlyScopeName }
                        }
                    ]);
                ConfigureIdentityServerResources(identityServerBuilder);
            },
            configurePipeline: webapp =>
            {
                _ = webapp.UseIdentityServer();
                _ = webapp.UseAuthorization();
                MapScimEndpoints(webapp);
            });
        return server;
    }

    private static readonly string[] ScimUserSchemas = ["urn:ietf:params:scim:schemas:core:2.0:User"];

    private static async Task<string> GetAccessTokenAsync(KestrelBasedTestServer server, string clientId)
    {
        using var client = server.CreateClient();
        using var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = "secret"
        }), Ct);
        _ = response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Ct), cancellationToken: Ct);
        return body.RootElement.GetProperty("access_token").GetString()!;
    }

    [Fact]
    public async Task ServiceProviderConfig_is_anonymous_and_hides_changePassword_when_disabled()
    {
        var server = CreateDefaultScimServer();
        await using var disposable = server;
        await server.StartAsync();
        await server.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        using var client = server.CreateClient();
        using var response = await client.GetAsync("/scim/ServiceProviderConfig", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("changePassword").GetProperty("supported").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Unauthenticated_request_to_Users_is_rejected()
    {
        var server = CreateDefaultScimServer();
        await using var disposable = server;
        await server.StartAsync();
        await server.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        using var client = server.CreateClient();
        using var response = await client.GetAsync("/scim/Users", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Read_only_token_can_list_but_not_create_Users()
    {
        var server = CreateDefaultScimServer();
        await using var disposable = server;
        await server.StartAsync();
        await server.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var token = await GetAccessTokenAsync(server, "scim-read-only");
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var listResponse = await client.GetAsync("/scim/Users", Ct);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var createContent = new StringContent(
            JsonSerializer.Serialize(new
            {
                schemas = ScimUserSchemas,
                userName = "erin",
                emails = new[] { new { value = "erin@example.com", primary = true } }
            }),
            Encoding.UTF8,
            "application/scim+json");
        using var createResponse = await client.PostAsync("/scim/Users", createContent, Ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Full_access_token_can_create_Users()
    {
        var server = CreateDefaultScimServer();
        await using var disposable = server;
        await server.StartAsync();
        await server.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var token = await GetAccessTokenAsync(server, "scim-full-access");
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var createContent = new StringContent(
            JsonSerializer.Serialize(new
            {
                schemas = ScimUserSchemas,
                userName = "erin",
                emails = new[] { new { value = "erin@example.com", primary = true } }
            }),
            Encoding.UTF8,
            "application/scim+json");
        using var createResponse = await client.PostAsync("/scim/Users", createContent, Ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public void MapScim_without_EnableScim_throws()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddUserManagement(_ => { });

        var builder = WebApplication.CreateBuilder();
        _ = builder.Services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddUserManagement(_ => { });

        using var app = builder.Build();

        _ = Should.Throw<InvalidOperationException>(() => app.MapScim());
    }

    [Fact]
    public async Task Default_policies_startup_succeeds_without_Authority_or_IssuerUri_and_a_scim_token_reads()
    {
        var server = CreateDefaultScimServer();
        await using var disposable = server;
        await server.StartAsync();
        await server.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        // Startup succeeds without Authority or IssuerUri: SCIM validates tokens in-process
        // against IdentityServer's own issuer and signing keys.
        _ = server.GetRequiredService<IOptions<ScimOAuthOptions>>().Value;

        var token = await GetAccessTokenAsync(server, "scim-full-access");
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/scim/Users", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Custom_policy_accepts_a_valid_key_and_rejects_an_invalid_one()
    {
        var server = new KestrelBasedTestServer(
            serverName: "scim-custom-" + Guid.NewGuid().ToString("N"),
            fixture: testServer,
            output: TestContext.Current.TestOutputHelper!,
            configureServices: services =>
            {
                RegisterCustomPolicy(services);
                _ = services.AddIdentityServer()
                    .AddStorage(s => s.AddSqliteInMemory())
                    .AddConfigurationStorage()
                    .AddOperationalStorage()
                    .AddUserManagement(ConfigureCustomPolicy);
            },
            configurePipeline: webapp =>
            {
                _ = webapp.UseIdentityServer();
                _ = webapp.UseAuthorization();
                MapScimEndpoints(webapp);
            });
        await using var disposable = server;
        await server.StartAsync();
        await server.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        // Metadata stays anonymous even with a custom policy.
        using var anonymousClient = server.CreateClient();
        using var metadataResponse = await anonymousClient.GetAsync("/scim/ServiceProviderConfig", Ct);
        metadataResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // No key at all: rejected.
        using var noKeyResponse = await anonymousClient.GetAsync("/scim/Users", Ct);
        noKeyResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Valid key: accepted.
        using var acceptedClient = server.CreateClient();
        acceptedClient.DefaultRequestHeaders.Add("X-Api-Key", ValidApiKey);
        using var acceptedResponse = await acceptedClient.GetAsync("/scim/Users", Ct);
        acceptedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Invalid key: the scheme authenticates but without the required scope claim, so it's forbidden.
        using var rejectedClient = server.CreateClient();
        rejectedClient.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");
        using var rejectedResponse = await rejectedClient.GetAsync("/scim/Users", Ct);
        rejectedResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Custom_policy_startup_succeeds_without_Authority()
    {
        var server = new KestrelBasedTestServer(
            serverName: "scim-custom-startup-" + Guid.NewGuid().ToString("N"),
            fixture: testServer,
            output: TestContext.Current.TestOutputHelper!,
            configureServices: services =>
            {
                RegisterCustomPolicy(services);
                _ = services.AddIdentityServer()
                    .AddStorage(s => s.AddSqliteInMemory())
                    .AddConfigurationStorage()
                    .AddOperationalStorage()
                    .AddUserManagement(ConfigureCustomPolicy);
            },
            configurePipeline: webapp =>
            {
                _ = webapp.UseIdentityServer();
                _ = webapp.UseAuthorization();
                MapScimEndpoints(webapp);
            });
        await using var disposable = server;

        await server.StartAsync();
        await server.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        _ = server.GetRequiredService<IOptions<ScimOAuthOptions>>().Value;
    }

    /// <summary>
    /// Minimal authentication handler standing in for a real host's own SCIM authentication scheme
    /// (for example <c>AddJwtBearer</c> against another authority). Authenticates any request carrying
    /// the expected <c>X-Api-Key</c> header value, issuing a <c>scope=scim</c> claim.
    /// </summary>
    private sealed class TestApiKeyHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Api-Key", out var key) || key.Count == 0)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = key == ValidApiKey
                ? new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim("scope", "scim")], CustomSchemeName)
                : new System.Security.Claims.ClaimsIdentity(CustomSchemeName); // Authenticated, but no scope claim: the policy forbids it.
            var ticket = new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), CustomSchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}

