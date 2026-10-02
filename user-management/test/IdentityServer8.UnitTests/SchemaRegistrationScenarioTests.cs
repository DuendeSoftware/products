// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Admin.IdentityProviders;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.Spaces;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IdentityServer8.UnitTests;

#pragma warning disable duende_experimental

public sealed class SchemaRegistrationScenarioTests
{
    private const string Password = "Sup3rSecre7!!";

    private static readonly AttributeDefinition Department = new()
    {
        Code = AttributeCode.Create("department"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String)
    };

    private static Ct Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task With_the_default_user_profile_a_user_can_log_in_by_email_and_receives_email_and_name_claims()
    {
        await using var sp = await BuildUserManagementHostAsync(b => b.AddUserManagement(_ => { }));

        var subjectId = await CreateUserWithPasswordAsync(sp, ("email", "alice@example.com"), ("name", "Alice Example"));

        (await LogInAsync(sp, "email", "alice@example.com")).ShouldBe(subjectId);
        var claims = await IssuedClaimsAsync(sp, subjectId, "email", "name");
        claims.ShouldContain(c => c.Type == "email" && c.Value == "alice@example.com");
        claims.ShouldContain(c => c.Type == "name" && c.Value == "Alice Example");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_attribute_the_customer_adds_to_the_default_user_profile_is_issued_as_a_claim_alongside_the_default_claims(
        bool customerSchemaRegisteredBeforeUserManagement)
    {
        await using var sp = await BuildUserManagementHostAsync(b =>
        {
            if (customerSchemaRegisteredBeforeUserManagement)
            {
                _ = b.AddInMemoryDataExtensionSchemas([BuiltInSchemas.UserProfile.Extend(Department)]);
            }
            _ = b.AddUserManagement(_ => { });
            if (!customerSchemaRegisteredBeforeUserManagement)
            {
                _ = b.AddInMemoryDataExtensionSchemas([BuiltInSchemas.UserProfile.Extend(Department)]);
            }
        });

        var subjectId = await CreateUserWithPasswordAsync(sp,
            ("email", "bob@example.com"), ("name", "Bob Example"), ("department", "engineering"));

        (await LogInAsync(sp, "email", "bob@example.com")).ShouldBe(subjectId);
        var claims = await IssuedClaimsAsync(sp, subjectId, "name", "department");
        claims.ShouldContain(c => c.Type == "department" && c.Value == "engineering");
        claims.ShouldContain(c => c.Type == "name" && c.Value == "Bob Example");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task When_the_customer_replaces_the_default_user_profile_users_log_in_by_username_and_no_name_claim_is_issued(
        bool customerSchemaRegisteredBeforeUserManagement)
    {
        var username = new AttributeDefinition
        {
            Code = AttributeCode.Create("username"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            IsUnique = true,
            IsRequired = true
        };
        var replacement = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            AttributeDefinitions = [username, Department]
        };
        await using var sp = await BuildUserManagementHostAsync(b =>
        {
            if (customerSchemaRegisteredBeforeUserManagement)
            {
                _ = b.AddInMemoryDataExtensionSchemas([replacement]);
            }
            _ = b.AddUserManagement(_ => { });
            if (!customerSchemaRegisteredBeforeUserManagement)
            {
                _ = b.AddInMemoryDataExtensionSchemas([replacement]);
            }
        });

        var subjectId = await CreateUserWithPasswordAsync(sp, ("username", "carol"), ("department", "sales"));

        (await LogInAsync(sp, "username", "carol")).ShouldBe(subjectId);
        var claims = await IssuedClaimsAsync(sp, subjectId, "name", "department");
        claims.ShouldContain(c => c.Type == "department" && c.Value == "sales");
        claims.ShouldNotContain(c => c.Type == "name");
    }

    [Fact]
    public async Task User_management_on_its_own_storage_instance_works_without_a_default_database()
    {
        var instance = StorageInstanceId.Create("user_management");
        var services = CreateServices();
        _ = services.AddIdentityServer()
            .AddStorage(instance, storage => storage.AddSqliteInMemory())
            .AddUserManagement(instance, _ => { });
        await using var sp = services.BuildServiceProvider();
        var schema = await sp.GetRequiredService<IStorageInstanceSchemaFactory>().GetStorageInstanceSchema(instance, Ct);
        await schema.MigrateAsync(Ct);

        var subjectId = await CreateUserWithPasswordAsync(sp, ("email", "dave@example.com"), ("name", "Dave Example"));

        (await LogInAsync(sp, "email", "dave@example.com")).ShouldBe(subjectId);
        var claims = await IssuedClaimsAsync(sp, subjectId, "email");
        claims.ShouldContain(c => c.Type == "email" && c.Value == "dave@example.com");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_admin_can_create_an_OIDC_identity_provider_in_a_host_with_user_management(
        bool userManagementAddedBeforeConfigurationStorage)
    {
        var services = CreateServices();
        var builder = services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory());
        if (userManagementAddedBeforeConfigurationStorage)
        {
            _ = builder.AddUserManagement(_ => { });
        }
        _ = builder.AddConfigurationStorage().AddOperationalStorage();
        if (!userManagementAddedBeforeConfigurationStorage)
        {
            _ = builder.AddUserManagement(_ => { });
        }

        await using var sp = services.BuildServiceProvider();
        var result = await CreateOidcIdentityProviderAsync(sp);

        result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_host_with_user_management_and_database_schemas_starts_and_offers_schema_administration(
        bool databaseSchemasAddedBeforeUserManagement)
    {
        var webBuilder = CreateValidatingBuilder();
        var builder = webBuilder.Services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage();
        if (databaseSchemasAddedBeforeUserManagement)
        {
            _ = builder.AddDynamicSchemas().AddUserManagement(_ => { });
        }
        else
        {
            _ = builder.AddUserManagement(_ => { }).AddDynamicSchemas();
        }

        await using var app = webBuilder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        await app.StartAsync(Ct);

        using var scope = app.Services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IUserProfileAdmin>();
        _ = scope.ServiceProvider.GetRequiredService<ISchemaAdmin>();
    }

    [Fact]
    public async Task A_host_with_user_management_and_spaces_starts()
    {
        var builder = CreateValidatingBuilder();
        _ = builder.Services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddUserManagement(_ => { });
        _ = builder.Services.AddSpaces();

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        await app.StartAsync(Ct);

        using var scope = app.Services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IUserProfileAdmin>();
    }

    [Fact]
    public async Task An_admin_can_create_a_client_in_a_space_when_schemas_are_stored_in_the_database()
    {
        var builder = CreateValidatingBuilder();
        _ = builder.Services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddDynamicSchemas();
        _ = builder.Services.AddSpaces();

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        await app.StartAsync(Ct);

        var created = await app.Services.GetRequiredService<ISpaceAdmin>().CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Tenant A",
                MatchPatterns = [new SpaceMatchPattern { Path = "/tenant-a" }]
            }, Ct);
        created.IsSuccess.ShouldBeTrue($"Create failed: {created}");

        using (app.Services.GetRequiredService<ISpaceContextAccessor>().SetSpace(created.Id))
        {
            using var scope = app.Services.CreateScope();
            var clientAdmin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();
            var clientId = $"client_{Guid.NewGuid():N}";

            var client = new CreateClient
            {
                ClientId = clientId,
                AllowedGrantTypes = [GrantType.ClientCredentials],
                ClientSecrets = [new CreateClientSecret { PlaintextValue = "secret" }]
            };

            var result = await clientAdmin.CreateAsync(client, Ct);

            result.IsSuccess.ShouldBeTrue($"Create failed: {result}");
            var read = await clientAdmin.GetAsync(result.Id, Ct);
            read.Found.ShouldBeTrue();
            read.Item.ClientId.ShouldBe(clientId);
        }
    }

    [Fact]
    public async Task A_space_stores_and_returns_an_extended_property_defined_in_the_customers_space_schema()
    {
        var services = CreateServices();
        _ = services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddInMemoryDataExtensionSchemas([new SchemaConfiguration
            {
                SchemaId = SchemaId.Space,
                AttributeDefinitions =
                [
                    new AttributeDefinition
                    {
                        Code = AttributeCode.Create("tier"),
                        AttributeType = new ScalarAttributeType(ScalarDataType.String)
                    }
                ]
            }]);
        _ = services.AddSpaces();
        await using var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        var spaceAdmin = sp.GetRequiredService<ISpaceAdmin>();
        var extendedProperties = new AttributeValueCollection();
        extendedProperties.Set(AttributeCode.Create("tier"), "gold");

        var created = await spaceAdmin.CreateAsync(new CreateSpaceConfiguration
        {
            Name = "test-space",
            MatchPatterns = [new SpaceMatchPattern { Origin = "https://tenant-a.example.com" }],
            ExtendedProperties = extendedProperties
        }, Ct);
        created.IsSuccess.ShouldBeTrue($"Create failed: {created}");

        var space = await spaceAdmin.GetAsync(created.Id, Ct);
        space.Found.ShouldBeTrue();
        space.Item!.ExtendedProperties
            .Single(p => p.Code == AttributeCode.Create("tier"))
            .UntypedValue.ShouldBe("gold");
    }

    [Fact(Skip = "Deferred: the default user profile is OIDC-shaped even with SCIM enabled; see the SCIM default-profile follow-up (schema-registration-redesign plan, follow-up 5).")]
    public async Task A_user_provisioned_through_scim_receives_email_and_name_claims()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        _ = builder.Services.AddAuthentication();
        _ = builder.Services.AddAuthorization(o => o.AddPolicy("scim-open", p => p.RequireAssertion(_ => true)));
        _ = builder.Services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddUserManagement(um =>
            {
                _ = um.EnableScim(_ => { });
                _ = um.ConfigureScimOAuth(o => o.AuthorizationPolicyName = "scim-open");
            });
        await using var app = builder.Build();
        _ = app.UseAuthorization();
        _ = app.MapScim();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        await app.StartAsync(Ct);

        using var http = app.GetTestClient();
        using var content = new StringContent(
            JsonSerializer.Serialize(new
            {
                schemas = new List<string> { "urn:ietf:params:scim:schemas:core:2.0:User" },
                userName = "erin",
                emails = new List<object> { new { value = "erin@example.com", primary = true } },
                name = new { formatted = "Erin Example" }
            }),
            Encoding.UTF8,
            "application/scim+json");
        using var response = await http.PostAsync("/scim/Users", content, Ct);
        _ = response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var subjectId = UserSubjectId.Create(body.RootElement.GetProperty("id").GetString()!);

        var claims = await IssuedClaimsAsync(app.Services, subjectId, "email", "name");

        claims.ShouldContain(c => c.Type == "email" && c.Value == "erin@example.com");
        claims.ShouldContain(c => c.Type == "name" && c.Value == "Erin Example");
    }

    private static WebApplicationBuilder CreateValidatingBuilder()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        _ = builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateOnBuild = true;
            o.ValidateScopes = true;
        });
        return builder;
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        _ = services.AddLogging();
        return services;
    }

    private static async Task<SaveResult<IdentityProviderId>> CreateOidcIdentityProviderAsync(IServiceProvider sp)
    {
        await sp.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var provider = new CreateIdentityProvider
        {
            Scheme = $"provider_{Guid.NewGuid():N}",
            Type = "oidc"
        };
        provider.ExtendedProperties.Set(AttributeCode.Create("Authority"), "https://example.com");
        provider.ExtendedProperties.Set(AttributeCode.Create("ClientId"), "client-id");

        return await sp.GetRequiredService<IIdentityProviderAdmin>().CreateAsync(provider, Ct);
    }

    private static async Task<ServiceProvider> BuildUserManagementHostAsync(Action<IIdentityServerBuilder> compose)
    {
        var services = CreateServices();
        compose(services.AddIdentityServer()
            .AddStorage(s => s.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage());

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        return sp;
    }

    private static async Task<UserSubjectId> CreateUserWithPasswordAsync(
        IServiceProvider sp, params (string Code, string Value)[] values)
    {
        var profileAdmin = sp.GetRequiredService<IUserProfileAdmin>();
        var authenticatorsSelfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var subjectId = (await sp.GetRequiredService<IExternalAuthenticator>().TryAuthenticateAsync(
                new ExternalAuthenticatorAddress(ExternalAuthenticatorName.Create("test-ext"), OpaqueSubjectId.Create(Guid.NewGuid().ToString())), Ct))
            .ShouldBeOfType<ExternalAuthenticationResult.Success>().UserSubjectId;

        var attributes = new AttributeValueCollection(await profileAdmin.GetSchemaAsync(Ct));
        foreach (var (code, value) in values)
        {
            attributes.Set(AttributeCode.Create(code), value);
        }
        _ = (await profileAdmin.TryAddAsync(subjectId, attributes.Validate(), Ct)).ShouldNotBeNull();

        var password = await authenticatorsSelfService.ValidatePasswordAsync(subjectId, Password, Ct);
        (await authenticatorsSelfService.TrySetPasswordAsync(subjectId, password, Ct)).ShouldBeTrue();
        return subjectId;
    }

    private static async Task<UserSubjectId> LogInAsync(IServiceProvider sp, string loginAttribute, string loginValue) =>
        (await sp.GetRequiredService<IPasswordAuthenticator>().TryAuthenticateAsync(
            AttributeCode.Create(loginAttribute), loginValue, NonValidatedPassword.Create(Password), Ct))
        .ShouldBeOfType<PasswordAuthenticationResult.Success>().UserSubjectId;

    private static async Task<List<Claim>> IssuedClaimsAsync(
        IServiceProvider sp, UserSubjectId subjectId, params string[] claimTypes)
    {
        var context = new ProfileDataRequestContext(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, subjectId.ToString())])),
            new Client(),
            "test",
            claimTypes);
        await sp.GetRequiredService<IProfileService>().GetProfileDataAsync(context, Ct);
        return context.IssuedClaims;
    }
}
