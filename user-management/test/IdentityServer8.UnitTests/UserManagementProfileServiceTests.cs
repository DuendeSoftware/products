// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.UserManagement;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityServer8.UnitTests;

public sealed class UserManagementProfileServiceTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private ServiceProvider _sp = null!;
    private IExternalAuthenticator _externalAuthenticator = null!;
    private IUserProfileAdmin _userProfileAdmin = null!;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private IUserAuthenticatorsAdmin _authenticatorsAdmin = null!;
    private IMembershipAdmin _membershipAdmin = null!;
    private IRoleAdmin _roleAdmin = null!;
    private IGroupAdmin _groupAdmin = null!;
    private UserManagementProfileService _sut = null!;

    public async ValueTask InitializeAsync()
    {
        _sp = await UsersServiceProviderFactory.CreateAsync();
        _externalAuthenticator = _sp.GetRequiredService<IExternalAuthenticator>();
        _userProfileAdmin = _sp.GetRequiredService<IUserProfileAdmin>();
        _schemaAdmin = _sp.GetRequiredService<IUserProfileSchemaAdmin>();
        _authenticatorsAdmin = _sp.GetRequiredService<IUserAuthenticatorsAdmin>();
        _membershipAdmin = _sp.GetRequiredService<IMembershipAdmin>();
        _roleAdmin = _sp.GetRequiredService<IRoleAdmin>();
        _groupAdmin = _sp.GetRequiredService<IGroupAdmin>();
        _sut = new UserManagementProfileService(NullLogger<UserManagementProfileService>.Instance, _authenticatorsAdmin, _userProfileAdmin, _membershipAdmin);
    }

    public ValueTask DisposeAsync() => _sp.DisposeAsync();

    private static ClaimsPrincipal MakeSubject(string sub) =>
        new(new ClaimsIdentity([new Claim(JwtClaimTypes.Subject, sub)]));

    private static Client EmptyClient => new();

    private static ProfileDataRequestContext MakeProfileContext(string sub, params string[] requestedClaimTypes) =>
        new(MakeSubject(sub), EmptyClient, "test", requestedClaimTypes);

    private static IsActiveContext MakeIsActiveContext(string sub) =>
        new(MakeSubject(sub), EmptyClient, "test");

    private async Task DefineStringAttribute(string code) =>
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create(code),
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create(code)
            },
            _ct)).ShouldBeTrue();

    private async Task DefineBoolAttribute(string code) =>
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create(code),
                AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
                Description = AttributeDescription.Create(code)
            },
            _ct)).ShouldBeTrue();

    private async Task<UserSubjectId> CreateUserWithStringAttributes(params (string code, string value)[] attrs)
    {
        var schema = await _userProfileAdmin.GetSchemaAsync(_ct);
        var attributes = new AttributeValueCollection(schema);
        foreach (var (code, value) in attrs)
        {
            attributes.Set(AttributeCode.Create(code), value);
        }

        var subjectId = UserSubjectId.New();
        var profile = await _userProfileAdmin.TryAddAsync(subjectId, attributes.Validate(), _ct);
        _ = profile.ShouldNotBeNull();
        return subjectId;
    }

    private async Task<UserSubjectId> CreateUserWithBoolAttributes(params (string code, bool value)[] attrs)
    {
        var schema = await _userProfileAdmin.GetSchemaAsync(_ct);
        var attributes = new AttributeValueCollection(schema);
        foreach (var (code, value) in attrs)
        {
            attributes.Set(AttributeCode.Create(code), value);
        }

        var subjectId = UserSubjectId.New();
        var profile = await _userProfileAdmin.TryAddAsync(subjectId, attributes.Validate(), _ct);
        _ = profile.ShouldNotBeNull();
        return subjectId;
    }

    private async Task<UserSubjectId> CreateEmptyUser()
    {
        var externalAuthenticatorAddress = new ExternalAuthenticatorAddress(
            ExternalAuthenticatorName.Create("test-ext"), OpaqueSubjectId.Create(Guid.NewGuid().ToString()));

        var subjectId = (await _externalAuthenticator.TryAuthenticateAsync(externalAuthenticatorAddress, _ct))
            .ShouldBeOfType<ExternalAuthenticationResult.Success>().UserSubjectId;

        var profile = await _userProfileAdmin.TryAddAsync(subjectId, ValidatedAttributeValueCollection.Empty, _ct);
        _ = profile.ShouldNotBeNull();
        return subjectId;
    }

    private async Task<RoleId> CreateRole(string name)
    {
        var result = await _roleAdmin.CreateAsync(new Role { Name = RoleName.Create(name) }, _ct);
        result.IsSuccess.ShouldBeTrue();
        return result.Id!.Value;
    }

    private async Task<GroupId> CreateGroup(string name)
    {
        var result = await _groupAdmin.CreateAsync(new Group { Name = GroupName.Create(name) }, _ct);
        result.IsSuccess.ShouldBeTrue();
        return result.Id!.Value;
    }

    // --- Tests ---

    [Fact]
    public async Task GetProfileDataAsync_maps_attributes_to_claims()
    {
        await DefineStringAttribute("email");
        await DefineStringAttribute("given_name");

        var subjectId = await CreateUserWithStringAttributes(
            ("email", "bob@example.com"),
            ("given_name", "Bob"));

        var ctx = MakeProfileContext(subjectId.ToString(), "email", "given_name");
        await _sut.GetProfileDataAsync(ctx, _ct);

        ctx.IssuedClaims.ShouldContain(c => c.Type == "email" && c.Value == "bob@example.com");
        ctx.IssuedClaims.ShouldContain(c => c.Type == "given_name" && c.Value == "Bob");
    }

    [Fact]
    public async Task GetProfileDataAsync_maps_boolean_attributes_with_lowercase_values()
    {
        await DefineBoolAttribute("email_verified");
        await DefineBoolAttribute("phone_number_verified");

        var subjectId = await CreateUserWithBoolAttributes(
            ("email_verified", true),
            ("phone_number_verified", false));

        var ctx = MakeProfileContext(subjectId.ToString(), "email_verified", "phone_number_verified");
        await _sut.GetProfileDataAsync(ctx, _ct);

        var emailVerified = ctx.IssuedClaims.Single(c => c.Type == "email_verified");
        emailVerified.Value.ShouldBe("true");
        emailVerified.ValueType.ShouldBe(ClaimValueTypes.Boolean);

        var phoneVerified = ctx.IssuedClaims.Single(c => c.Type == "phone_number_verified");
        phoneVerified.Value.ShouldBe("false");
        phoneVerified.ValueType.ShouldBe(ClaimValueTypes.Boolean);
    }

    [Fact]
    public async Task GetProfileDataAsync_emits_role_claims_for_direct_and_transitive_roles()
    {
        var subjectId = await CreateEmptyUser();

        var adminRoleId = await CreateRole("admin");
        var editorRoleId = await CreateRole("editor");

        // Assign admin directly
        _ = await _membershipAdmin.AssignRoleAsync(subjectId, adminRoleId, _ct);

        // Assign editor transitively via a group
        var groupId = await CreateGroup("editors");
        _ = await _membershipAdmin.AssignGroupAsync(subjectId, groupId, _ct);
        _ = await _membershipAdmin.AssignRoleToGroupAsync(editorRoleId, groupId, _ct);

        var ctx = MakeProfileContext(subjectId.ToString(), JwtClaimTypes.Role);
        await _sut.GetProfileDataAsync(ctx, _ct);

        ctx.IssuedClaims.ShouldContain(c => c.Type == JwtClaimTypes.Role && c.Value == "admin");
        ctx.IssuedClaims.ShouldContain(c => c.Type == JwtClaimTypes.Role && c.Value == "editor");
    }

    [Fact]
    public async Task GetProfileDataAsync_deduplicates_roles_when_role_appears_both_directly_and_transitively()
    {
        var subjectId = await CreateEmptyUser();

        var adminRoleId = await CreateRole("admin");

        // Assign directly
        _ = await _membershipAdmin.AssignRoleAsync(subjectId, adminRoleId, _ct);

        // Also assign transitively via group
        var groupId = await CreateGroup("admins");
        _ = await _membershipAdmin.AssignGroupAsync(subjectId, groupId, _ct);
        _ = await _membershipAdmin.AssignRoleToGroupAsync(adminRoleId, groupId, _ct);

        var ctx = MakeProfileContext(subjectId.ToString(), JwtClaimTypes.Role);
        await _sut.GetProfileDataAsync(ctx, _ct);

        ctx.IssuedClaims.Count(c => c.Type == JwtClaimTypes.Role && c.Value == "admin").ShouldBe(1);
    }

    [Fact]
    public async Task GetProfileDataAsync_when_user_not_found_issues_no_claims()
    {
        var nonExistentId = UserSubjectId.New();
        var ctx = MakeProfileContext(nonExistentId.ToString(), "email");

        await _sut.GetProfileDataAsync(ctx, _ct);

        ctx.IssuedClaims.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetProfileDataAsync_when_subject_id_not_a_guid_issues_no_claims()
    {
        var ctx = MakeProfileContext("not-a-guid", "email");

        await _sut.GetProfileDataAsync(ctx, _ct);

        ctx.IssuedClaims.ShouldBeEmpty();
    }

    [Fact]
    public async Task IsActiveAsync_when_user_exists_is_active()
    {
        var subjectId = await CreateEmptyUser();
        var ctx = MakeIsActiveContext(subjectId.ToString());

        await _sut.IsActiveAsync(ctx, _ct);

        ctx.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task IsActiveAsync_when_user_not_found_is_not_active()
    {
        var nonExistentId = UserSubjectId.New();
        var ctx = MakeIsActiveContext(nonExistentId.ToString());

        await _sut.IsActiveAsync(ctx, _ct);

        ctx.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task IsActiveAsync_when_subject_id_not_a_guid_is_not_active()
    {
        var ctx = MakeIsActiveContext("not-a-guid");

        await _sut.IsActiveAsync(ctx, _ct);

        ctx.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task GetProfileDataAsync_when_subject_id_is_non_v4_guid_issues_no_claims()
    {
        // Version nibble is 1, not 4 — this is a v1 UUID
        var ctx = MakeProfileContext("12345678-1234-1234-8234-123456789012", "email");

        await _sut.GetProfileDataAsync(ctx, _ct);

        ctx.IssuedClaims.ShouldBeEmpty();
    }

    [Fact]
    public async Task IsActiveAsync_when_subject_id_is_non_v4_guid_is_not_active()
    {
        // Version nibble is 1, not 4 — this is a v1 UUID
        var ctx = MakeIsActiveContext("12345678-1234-1234-8234-123456789012");

        await _sut.IsActiveAsync(ctx, _ct);

        ctx.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task GetProfileDataAsync_maps_all_scalar_types_with_correct_claim_value_types()
    {
        // Define attributes for each scalar type
        await DefineAttribute("age", new ScalarAttributeType(ScalarDataType.Integer));
        await DefineAttribute("balance", new ScalarAttributeType(ScalarDataType.Decimal));
        await DefineAttribute("birthday", new ScalarAttributeType(ScalarDataType.Date));
        await DefineAttribute("registered_at", new ScalarAttributeType(ScalarDataType.DateTime));
        await DefineAttribute("nickname", new ScalarAttributeType(ScalarDataType.String));
        await DefineBoolAttribute("active");

        var schema = await _userProfileAdmin.GetSchemaAsync(_ct);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(AttributeCode.Create("age"), 42);
        attributes.Set(AttributeCode.Create("balance"), 123.45m);
        attributes.Set(AttributeCode.Create("birthday"), new DateOnly(1990, 6, 15));
        attributes.Set(AttributeCode.Create("registered_at"), new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero));
        attributes.Set(AttributeCode.Create("nickname"), "Bobby");
        attributes.Set(AttributeCode.Create("active"), true);

        var subjectId = UserSubjectId.New();
        _ = (await _userProfileAdmin.TryAddAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var ctx = MakeProfileContext(subjectId.ToString(), "age", "balance", "birthday", "registered_at", "nickname", "active");
        await _sut.GetProfileDataAsync(ctx, _ct);

        var ageClaim = ctx.IssuedClaims.Single(c => c.Type == "age");
        ageClaim.Value.ShouldBe("42");
        ageClaim.ValueType.ShouldBe(ClaimValueTypes.Integer32);

        var balanceClaim = ctx.IssuedClaims.Single(c => c.Type == "balance");
        balanceClaim.Value.ShouldBe("123.45");
        balanceClaim.ValueType.ShouldBe(ClaimValueTypes.Double);

        var birthdayClaim = ctx.IssuedClaims.Single(c => c.Type == "birthday");
        birthdayClaim.Value.ShouldBe("1990-06-15");
        birthdayClaim.ValueType.ShouldBe(ClaimValueTypes.Date);

        var registeredClaim = ctx.IssuedClaims.Single(c => c.Type == "registered_at");
        registeredClaim.Value.ShouldBe("2024-01-15T10:30:00.0000000+00:00");
        registeredClaim.ValueType.ShouldBe(ClaimValueTypes.DateTime);

        var nicknameClaim = ctx.IssuedClaims.Single(c => c.Type == "nickname");
        nicknameClaim.Value.ShouldBe("Bobby");
        nicknameClaim.ValueType.ShouldBe(ClaimValueTypes.String);

        var activeClaim = ctx.IssuedClaims.Single(c => c.Type == "active");
        activeClaim.Value.ShouldBe("true");
        activeClaim.ValueType.ShouldBe(ClaimValueTypes.Boolean);
    }

    [Fact]
    public async Task GetProfileDataAsync_maps_list_attribute_as_multiple_claims()
    {
        await DefineAttribute("tag", new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)));

        var schema = await _userProfileAdmin.GetSchemaAsync(_ct);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(AttributeCode.Create("tag"), ["alpha", "beta", "gamma"]);

        var subjectId = UserSubjectId.New();
        _ = (await _userProfileAdmin.TryAddAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var ctx = MakeProfileContext(subjectId.ToString(), "tag");
        await _sut.GetProfileDataAsync(ctx, _ct);

        var tagClaims = ctx.IssuedClaims.Where(c => c.Type == "tag").ToList();
        tagClaims.Count.ShouldBe(3);
        tagClaims.Select(c => c.Value).ShouldBe(["alpha", "beta", "gamma"], ignoreOrder: true);
        tagClaims.ShouldAllBe(c => c.ValueType == ClaimValueTypes.String);
    }

    [Fact]
    public async Task GetProfileDataAsync_skips_complex_attribute_types()
    {
        await DefineAttribute("address", new ComplexAttributeType(
            new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("street")] = ComplexAttributeProperty.Of(ScalarDataType.String)
            }));
        await DefineStringAttribute("name");

        var schema = await _userProfileAdmin.GetSchemaAsync(_ct);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(AttributeCode.Create("address"),
            new Dictionary<string, object> { ["street"] = "123 Main St" });
        attributes.Set(AttributeCode.Create("name"), "Alice");

        var subjectId = UserSubjectId.New();
        _ = (await _userProfileAdmin.TryAddAsync(subjectId, attributes.Validate(), _ct)).ShouldNotBeNull();

        var ctx = MakeProfileContext(subjectId.ToString(), "address", "name");
        await _sut.GetProfileDataAsync(ctx, _ct);

        ctx.IssuedClaims.ShouldNotContain(c => c.Type == "address");
        ctx.IssuedClaims.ShouldContain(c => c.Type == "name" && c.Value == "Alice");
    }

    private async Task DefineAttribute(string code, AttributeType attributeType) =>
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create(code),
                AttributeType = attributeType,
                Description = AttributeDescription.Create(code)
            },
            _ct)).ShouldBeTrue();

    [Fact]
    public void DI_resolves_UserManagementProfileService_when_AddUserManagement_is_configured()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IConfiguration>(new ConfigurationRoot([]));
        _ = services.AddLogging();
        _ = services
            .AddIdentityServer()
            .AddUserManagement(u => u.AddSqliteInMemoryStore());

        using var sp = services.BuildServiceProvider();

        var profileService = sp.GetRequiredService<Duende.IdentityServer.Services.IProfileService>();
        _ = profileService.ShouldBeOfType<UserManagementProfileService>();
    }
}
