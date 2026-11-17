// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.UserManagement;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityServer8.UnitTests;

public sealed class DefineUserProfileHowToTests
{
    private static Ct Ct => TestContext.Current.CancellationToken;

    private static readonly AttributeDefinition Department = new()
    {
        Code = AttributeCode.Create("department"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String)
    };

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithExtendedSchema(WebApplicationBuilder builder)
    {
        // begin-snippet: profile-extend-default-schema
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddInMemoryDataExtensionSchemas([BuiltInSchemas.UserProfile.Extend(Department)])
            .AddUserManagement(_ => { });
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task An_attribute_added_to_the_default_schema_is_issued_as_a_claim_alongside_the_defaults()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithExtendedSchema(builder);
        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var profileAdmin = app.Services.GetRequiredService<IUserProfileAdmin>();
        var subjectId = UserSubjectId.New();
        var attributes = new AttributeValueCollection(await profileAdmin.GetSchemaAsync(Ct));
        attributes.Set(AttributeCode.Create("email"), "finn@example.com");
        attributes.Set(AttributeCode.Create("department"), "engineering");
        _ = (await profileAdmin.TryAddAsync(subjectId, attributes.Validate(), Ct)).ShouldNotBeNull();

        var claims = await IssuedClaimsAsync(app.Services, subjectId, "email", "department");

        claims.ShouldContain(c => c.Type == "email" && c.Value == "finn@example.com");
        claims.ShouldContain(c => c.Type == "department" && c.Value == "engineering");
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static AttributeDefinition DefineAttribute()
    {
        // begin-snippet: profile-define-attribute
        var department = new AttributeDefinition
        {
            Code = AttributeCode.Create("department"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("The department the user belongs to.")
        };
        // end-snippet
        return department;
    }
#pragma warning restore IDE0022

    [Fact]
    public void Defining_an_attribute_produces_the_expected_definition()
    {
        var department = DefineAttribute();

        department.Code.ShouldBe(AttributeCode.Create("department"));
        department.AttributeType.ShouldBe(new ScalarAttributeType(ScalarDataType.String));
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithDynamicSchema(WebApplicationBuilder builder)
    {
        // begin-snippet: profile-dynamic-schema
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddDynamicSchemas()
            .AddUserManagement(_ => { });
        // end-snippet
    }

    private static async Task<SaveResult<SchemaId>> ReplaceProfileSchemaAsync(ISchemaAdmin schemaAdmin, Ct ct)
    {
        // begin-snippet: profile-dynamic-schema-replace
        var current = await schemaAdmin.GetAsync(SchemaId.UserProfile, ct);

        if (!current.Found)
        {
            return await schemaAdmin.CreateAsync(BuiltInSchemas.UserProfile.Extend(Department), ct);
        }

        // Extend throws if the attribute is already present, so this check makes the setup safe to run repeatedly
        // (e.g. on every app startup) against a store a previous run already extended.
        if (current.Item!.AttributeDefinitions.Any(a => a.Code == Department.Code))
        {
            return await schemaAdmin.UpdateAsync(SchemaId.UserProfile, current.Item!, current.Version!.Value, ct);
        }

        var replaced = await schemaAdmin.UpdateAsync(SchemaId.UserProfile, current.Item!.Extend(Department), current.Version!.Value, ct);
        // end-snippet
        return replaced;
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task A_schema_registered_through_ISchemaAdmin_can_be_edited_at_runtime()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithDynamicSchema(builder);
        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        using var scope = app.Services.CreateScope();
        var schemaAdmin = scope.ServiceProvider.GetRequiredService<ISchemaAdmin>();

        var replaced = await ReplaceProfileSchemaAsync(schemaAdmin, Ct);

        replaced.IsSuccess.ShouldBeTrue($"Update failed: {replaced}");
        var profileAdmin = scope.ServiceProvider.GetRequiredService<IUserProfileAdmin>();
        var schema = await profileAdmin.GetSchemaAsync(Ct);
        schema.AttributeDefinitions.ShouldContainKey(AttributeCode.Create("department"));
    }

    [Fact]
    public async Task Running_the_dynamic_schema_setup_twice_against_the_same_store_does_not_throw()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithDynamicSchema(builder);
        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        using var scope = app.Services.CreateScope();
        var schemaAdmin = scope.ServiceProvider.GetRequiredService<ISchemaAdmin>();

        var first = await ReplaceProfileSchemaAsync(schemaAdmin, Ct);
        var second = await ReplaceProfileSchemaAsync(schemaAdmin, Ct);

        first.IsSuccess.ShouldBeTrue($"First run failed: {first}");
        second.IsSuccess.ShouldBeTrue($"Second run failed: {second}");
        var profileAdmin = scope.ServiceProvider.GetRequiredService<IUserProfileAdmin>();
        var schema = await profileAdmin.GetSchemaAsync(Ct);
        schema.AttributeDefinitions.ShouldContainKey(AttributeCode.Create("department"));
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithCustomClaims(WebApplicationBuilder builder)
    {
        // begin-snippet: profile-custom-claims
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddProfileService<TenantClaimProfileService>()
            .AddUserManagement(_ => { });
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task A_subclass_of_UserManagementProfileService_adds_a_claim_the_base_class_does_not_issue()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithCustomClaims(builder);
        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var profileAdmin = app.Services.GetRequiredService<IUserProfileAdmin>();
        var subjectId = UserSubjectId.New();
        var attributes = new AttributeValueCollection(await profileAdmin.GetSchemaAsync(Ct));
        attributes.Set(AttributeCode.Create("email"), "grey@example.com");
        _ = (await profileAdmin.TryAddAsync(subjectId, attributes.Validate(), Ct)).ShouldNotBeNull();

        var claims = await IssuedClaimsAsync(app.Services, subjectId, "email", "tenant");

        claims.ShouldContain(c => c.Type == "tenant" && c.Value == "acme");
    }

    [Fact]
    public async Task A_user_with_no_authenticator_at_all_is_reported_as_inactive()
    {
        await using var host = await HowToIdentityServerHost.CreateAsync(_ => { });

        var profileAdmin = host.Services.GetRequiredService<IUserProfileAdmin>();
        var subjectId = UserSubjectId.New();
        var attributes = new AttributeValueCollection(await profileAdmin.GetSchemaAsync(Ct));
        attributes.Set(AttributeCode.Create("email"), "harper@example.com");
        _ = (await profileAdmin.TryAddAsync(subjectId, attributes.Validate(), Ct)).ShouldNotBeNull();

        var isActive = await IsActiveAsync(host.Services, subjectId);

        isActive.ShouldBeFalse();
    }

    private static async Task<List<Claim>> IssuedClaimsAsync(
        IServiceProvider sp, UserSubjectId subjectId, params string[] claimTypes)
    {
        var context = new ProfileDataRequestContext(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, subjectId.ToString())])),
            new Client(),
            "test",
            claimTypes);
        await sp.GetRequiredService<IProfileService>().GetProfileDataAsync(context, ct: Ct);
        return context.IssuedClaims;
    }

    private static async Task<bool> IsActiveAsync(IServiceProvider sp, UserSubjectId subjectId)
    {
        var context = new IsActiveContext(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, subjectId.ToString())])),
            new Client(),
            "test");
        await sp.GetRequiredService<IProfileService>().IsActiveAsync(context, Ct);
        return context.IsActive;
    }
}

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
/// <summary>
/// A custom claim source added in a subclass of <see cref="UserManagementProfileService"/>, registered with
/// <c>AddProfileService&lt;T&gt;()</c> before <c>AddUserManagement</c>.
/// </summary>
// begin-snippet: profile-custom-claims-service
internal sealed class TenantClaimProfileService(
    ILogger<TenantClaimProfileService> logger,
    IUserAuthenticatorsAdmin authenticatorsAdmin,
    IUserProfileAdmin profileAdmin,
    IMembershipAdmin membershipAdmin)
    : UserManagementProfileService(logger, authenticatorsAdmin, profileAdmin, membershipAdmin)
{
    protected override async Task GetProfileDataAsync(ProfileDataRequestContext context, UserProfile profile, Ct ct)
    {
        await base.GetProfileDataAsync(context, profile, ct);
        context.AddRequestedClaims([new Claim("tenant", "acme")]);
    }
}
// end-snippet
#pragma warning restore IDE0022
