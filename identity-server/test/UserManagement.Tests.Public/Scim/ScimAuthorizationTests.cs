// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.UserManagement;
using Duende.UserManagement.Scim;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimAuthorizationTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task ShouldReturn401WhenNoToken()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturn401WhenInvalidSignature()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateTokenWithWrongSignature("scim"));

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturn401WhenExpiredToken()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateExpiredToken("scim"));

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturn401WhenWrongAudience()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateTokenWithWrongAudience("scim"));

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturn403OnWriteEndpointWithOnlyReadScope()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateAccessToken("scim.read"));

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "forbidden-user" };
        var response = await client.PostAsync("/scim/Users", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ShouldReturn200OnReadEndpointWithReadScope()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateAccessToken("scim.read"));

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShouldReturn200OnReadEndpointWithFullScope()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateAccessToken("scim"));

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShouldHandleSpaceDelimitedScopeClaim()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateTokenWithSpaceDelimitedScopes("scim", "scim.read"));

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShouldReturn200OnMetadataEndpointsWithNoToken()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);

        var serviceProviderConfig = await client.GetAsync("/scim/ServiceProviderConfig");
        var resourceTypes = await client.GetAsync("/scim/ResourceTypes");
        var schemas = await client.GetAsync("/scim/Schemas");

        serviceProviderConfig.StatusCode.ShouldBe(HttpStatusCode.OK);
        resourceTypes.StatusCode.ShouldBe(HttpStatusCode.OK);
        schemas.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShouldIncludeWwwAuthenticateHeaderOn401()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ShouldNotBeEmpty();
        var scheme = response.Headers.WwwAuthenticate.First();
        scheme.Scheme.ShouldBe("Bearer");
    }

    [Fact]
    public async Task ShouldReturn403OnGroupWriteEndpointWithOnlyReadScope()
    {
        await Fixture.InitializeAsync();
        var groupClient = Fixture.BuildScimGroupClient(null);
        groupClient.SetBearerToken(ScimFixture.CreateAccessToken("scim.read"));

        var payload = new { schemas = new[] { ScimHttpClient.GroupSchemaUrn }, displayName = "forbidden-group" };
        var response = await groupClient.PostAsync("/scim/Groups", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ShouldReturn200OnGroupReadEndpointWithFullScope()
    {
        await Fixture.InitializeAsync();
        var groupClient = Fixture.BuildScimGroupClient(null);
        groupClient.SetBearerToken(ScimFixture.CreateAccessToken("scim"));

        var response = await groupClient.GetAsync("/scim/Groups");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Gap 1: Bulk endpoint authorization

    [Fact]
    public async Task ShouldReturn401OnBulkEndpointWithNoToken()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = Array.Empty<object>()
        };
        var response = await client.PostAsync(ScimHttpClient.BulkRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturn403OnBulkEndpointWithOnlyReadScope()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateAccessToken("scim.read"));

        var payload = new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = Array.Empty<object>()
        };
        var response = await client.PostAsync(ScimHttpClient.BulkRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Gap 3: Groups read with scim.read scope

    [Fact]
    public async Task ShouldReturn200OnGroupReadEndpointWithReadScope()
    {
        await Fixture.InitializeAsync();
        var groupClient = Fixture.BuildScimGroupClient(null);
        groupClient.SetBearerToken(ScimFixture.CreateAccessToken("scim.read"));

        var response = await groupClient.GetAsync("/scim/Groups");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Gap 5: .search endpoints auth

    [Fact]
    public async Task ShouldReturn401OnSearchEndpointWithNoToken()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);

        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn } };
        var response = await client.PostAsync("/scim/Users/.search", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturn200OnSearchEndpointWithReadScope()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateAccessToken("scim.read"));

        var payload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn } };
        var response = await client.PostAsync("/scim/Users/.search", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Gap 6: Per-ID endpoint auth

    [Fact]
    public async Task ShouldReturn401OnGetUserByIdWithNoToken()
    {
        await Fixture.InitializeAsync();
        var userId = await Fixture.CreateUserAsync("auth-test-user-get");

        var unauthClient = Fixture.BuildScimClient(null);
        var response = await unauthClient.GetAsync($"/scim/Users/{userId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturn403OnPutUserByIdWithOnlyReadScope()
    {
        await Fixture.InitializeAsync();
        var userId = await Fixture.CreateUserAsync("auth-test-user-put");

        var readOnlyClient = Fixture.BuildScimClient(null);
        readOnlyClient.SetBearerToken(ScimFixture.CreateAccessToken("scim.read"));

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "updated-name" };
        var response = await readOnlyClient.PutAsync($"/scim/Users/{userId}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ShouldReturn401OnDeleteUserByIdWithNoToken()
    {
        await Fixture.InitializeAsync();
        var userId = await Fixture.CreateUserAsync("auth-test-user-delete");

        var unauthClient = Fixture.BuildScimClient(null);
        var response = await unauthClient.DeleteAsync($"/scim/Users/{userId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // Gap 7: Metadata by-ID anonymous access

    [Fact]
    public async Task ShouldReturn200OnResourceTypeByIdWithNoToken()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);

        var response = await client.GetAsync("/scim/ResourceTypes/User");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShouldReturn200OnSchemaByIdWithNoToken()
    {
        await Fixture.InitializeAsync();
        var client = Fixture.BuildScimClient(null);

        var response = await client.GetAsync($"/scim/Schemas/{ScimHttpClient.UserSchemaUrn}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public async ValueTask DisposeAsync() => await Fixture.DisposeAsync();
}

// Gap 2: Custom AuthorizationPolicyName end-to-end

public sealed class ScimCustomPolicyAuthorizationTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private const string CustomPolicyName = "CustomScimPolicy";
    private const string RequiredClaimType = "custom_claim";

    [Fact]
    public async Task ShouldReturn401WithCustomPolicyWhenNoToken()
    {
        var fixture = CreateFixtureWithCustomPolicy(output, serverFixture);
        await fixture.InitializeAsync();
        var client = fixture.BuildScimClient(null);

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShouldReturn403WithCustomPolicyWhenMissingRequiredClaim()
    {
        var fixture = CreateFixtureWithCustomPolicy(output, serverFixture);
        await fixture.InitializeAsync();

        // Standard SCIM token has scope claims but not the custom_claim required by the policy
        var client = fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateAccessToken("scim"));

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ShouldReturn200WithCustomPolicyWhenRequiredClaimPresent()
    {
        var fixture = CreateFixtureWithCustomPolicy(output, serverFixture);
        await fixture.InitializeAsync();

        var client = fixture.BuildScimClient(null);
        client.SetBearerToken(ScimFixture.CreateTokenWithCustomClaims(
            new Claim("scope", "scim"),
            new Claim(RequiredClaimType, "allowed")));

        var response = await client.GetAsync("/scim/Users");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }

    private ScimFixture? _fixture;

    private ScimFixture CreateFixtureWithCustomPolicy(ITestOutputHelper testOutput, WebServerFixture server)
    {
        var fixture = new ScimFixture(testOutput, server);

        fixture.ConfigureScimAuthOptions = opts => opts.AuthorizationPolicyName = CustomPolicyName;

        fixture.ConfigureServices = services =>
        {
            _ = services.AddAuthorizationBuilder()
                .AddPolicy(CustomPolicyName, policy => policy
                    .RequireClaim(RequiredClaimType));
        };

        _fixture = fixture;
        return fixture;
    }
}

// Separate ReadAuthorizationPolicyName / WriteAuthorizationPolicyName

public sealed class ScimReadWritePolicyAuthorizationTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private const string RequiredClaimType = "custom_claim";

    public async ValueTask DisposeAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }

    private ScimFixture? _fixture;

    [Fact]
    public async Task OnlyReadPolicyName_ReadsUseCustomPolicy_WritesStillNeedScope()
    {
        const string readPolicyName = "CustomReadPolicy";
        var fixture = CreateFixture(output, serverFixture, opts => opts.ReadAuthorizationPolicyName = readPolicyName,
            (builder, _) => builder.AddPolicy(readPolicyName, p => p.RequireClaim(RequiredClaimType)));
        await fixture.InitializeAsync();

        // Reads (including .search) use the custom read policy: the "scim" scope alone is not enough.
        var noClaimClient = fixture.BuildScimClient(null);
        noClaimClient.SetBearerToken(ScimFixture.CreateAccessToken("scim"));
        (await noClaimClient.GetAsync("/scim/Users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var customClaimClient = fixture.BuildScimClient(null);
        customClaimClient.SetBearerToken(ScimFixture.CreateTokenWithCustomClaims(new Claim(RequiredClaimType, "allowed")));
        (await customClaimClient.GetAsync("/scim/Users")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var searchPayload = new { schemas = new[] { ScimHttpClient.SearchRequestSchemaUrn } };
        (await customClaimClient.PostAsync("/scim/Users/.search", ScimHttpClient.ScimJsonContent(searchPayload)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Writes still use the built-in scope-based policy.
        var writeClient = fixture.BuildScimClient(null);
        writeClient.SetBearerToken(ScimFixture.CreateAccessToken("scim.read"));
        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "forbidden-user" };
        (await writeClient.PostAsync("/scim/Users", ScimHttpClient.ScimJsonContent(payload)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var scimScopeClient = fixture.BuildScimClient(null);
        scimScopeClient.SetBearerToken(ScimFixture.CreateAccessToken("scim"));
        (await scimScopeClient.PostAsync("/scim/Users", ScimHttpClient.ScimJsonContent(payload)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task OnlyWritePolicyName_WritesUseCustomPolicy_ReadsStillNeedScope()
    {
        const string writePolicyName = "CustomWritePolicy";
        var fixture = CreateFixture(output, serverFixture, opts => opts.WriteAuthorizationPolicyName = writePolicyName,
            (builder, _) => builder.AddPolicy(writePolicyName, p => p.RequireClaim(RequiredClaimType)));
        await fixture.InitializeAsync();

        // Reads use the built-in scope-based policy.
        var readClient = fixture.BuildScimClient(null);
        readClient.SetBearerToken(ScimFixture.CreateAccessToken("scim.read"));
        (await readClient.GetAsync("/scim/Users")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Writes (including /Bulk) use the custom write policy: the "scim" scope alone is not enough.
        var noClaimClient = fixture.BuildScimClient(null);
        noClaimClient.SetBearerToken(ScimFixture.CreateAccessToken("scim"));
        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "forbidden-user" };
        (await noClaimClient.PostAsync("/scim/Users", ScimHttpClient.ScimJsonContent(payload)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var bulkPayload = new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = Array.Empty<object>()
        };
        (await noClaimClient.PostAsync(ScimHttpClient.BulkRoute, ScimHttpClient.ScimJsonContent(bulkPayload)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var customClaimClient = fixture.BuildScimClient(null);
        customClaimClient.SetBearerToken(ScimFixture.CreateTokenWithCustomClaims(new Claim(RequiredClaimType, "allowed")));
        (await customClaimClient.PostAsync("/scim/Users", ScimHttpClient.ScimJsonContent(payload)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task BothPolicyNamesSet_EachEndpointUsesItsOwnPolicy()
    {
        const string readPolicyName = "CustomReadPolicy2";
        const string writePolicyName = "CustomWritePolicy2";
        const string readClaim = "read_claim";
        const string writeClaim = "write_claim";

        // begin-snippet: scim-read-write-policy-names
        void ConfigureScimOAuth(ScimOAuthOptions opts)
        {
            // A read gets POST .../.search as well as GET. A write is POST, PUT, PATCH, DELETE,
            // and POST /Bulk. Each side falls back to AuthorizationPolicyName, then to the
            // built-in DuendeScimRead/DuendeScimWrite policy, when its own name isn't set.
            opts.ReadAuthorizationPolicyName = readPolicyName;
            opts.WriteAuthorizationPolicyName = writePolicyName;
        }

        void ConfigurePolicies(Microsoft.AspNetCore.Authorization.AuthorizationBuilder builder, IServiceCollection _)
        {
            builder.AddPolicy(readPolicyName, p => p.RequireClaim(readClaim));
            builder.AddPolicy(writePolicyName, p => p.RequireClaim(writeClaim));
        }
        // end-snippet

        var fixture = CreateFixture(output, serverFixture, ConfigureScimOAuth, ConfigurePolicies);
        await fixture.InitializeAsync();

        var readClient = fixture.BuildScimClient(null);
        readClient.SetBearerToken(ScimFixture.CreateTokenWithCustomClaims(new Claim(readClaim, "allowed")));
        (await readClient.GetAsync("/scim/Users")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "write-test-user" };
        (await readClient.PostAsync("/scim/Users", ScimHttpClient.ScimJsonContent(payload)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var writeClient = fixture.BuildScimClient(null);
        writeClient.SetBearerToken(ScimFixture.CreateTokenWithCustomClaims(new Claim(writeClaim, "allowed")));
        (await writeClient.PostAsync("/scim/Users", ScimHttpClient.ScimJsonContent(payload)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await writeClient.GetAsync("/scim/Users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SpecificNamePlusSharedName_SpecificWinsOnItsSide_SharedWinsOnTheOther()
    {
        const string sharedPolicyName = "SharedPolicy";
        const string readPolicyName = "SpecificReadPolicy";
        const string sharedClaim = "shared_claim";
        const string readClaim = "specific_read_claim";
        var fixture = CreateFixture(
            output,
            serverFixture,
            opts =>
            {
                opts.AuthorizationPolicyName = sharedPolicyName;
                opts.ReadAuthorizationPolicyName = readPolicyName;
            },
            (builder, _) =>
            {
                builder.AddPolicy(sharedPolicyName, p => p.RequireClaim(sharedClaim));
                builder.AddPolicy(readPolicyName, p => p.RequireClaim(readClaim));
            });
        await fixture.InitializeAsync();

        // Reads: the specific read policy wins, not the shared one.
        var sharedClaimClient = fixture.BuildScimClient(null);
        sharedClaimClient.SetBearerToken(ScimFixture.CreateTokenWithCustomClaims(new Claim(sharedClaim, "allowed")));
        (await sharedClaimClient.GetAsync("/scim/Users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var readClaimClient = fixture.BuildScimClient(null);
        readClaimClient.SetBearerToken(ScimFixture.CreateTokenWithCustomClaims(new Claim(readClaim, "allowed")));
        (await readClaimClient.GetAsync("/scim/Users")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Writes: no specific write policy is set, so the shared policy applies.
        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "write-test-user" };
        (await readClaimClient.PostAsync("/scim/Users", ScimHttpClient.ScimJsonContent(payload)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await sharedClaimClient.PostAsync("/scim/Users", ScimHttpClient.ScimJsonContent(payload)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private ScimFixture CreateFixture(
        ITestOutputHelper testOutput,
        WebServerFixture server,
        Action<ScimOAuthOptions> configureOptions,
        Action<Microsoft.AspNetCore.Authorization.AuthorizationBuilder, IServiceCollection> configurePolicies)
    {
        var fixture = new ScimFixture(testOutput, server);

        fixture.ConfigureScimAuthOptions = configureOptions;

        fixture.ConfigureServices = services =>
        {
            var builder = services.AddAuthorizationBuilder();
            configurePolicies(builder, services);
        };

        _fixture = fixture;
        return fixture;
    }
}
