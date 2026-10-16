// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Passkeys.Fixtures;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class StorageCategoryTests
{
    [Fact]
    public async Task User_admin_uses_correct_storage_category()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        var admin = serviceProvider.GetRequiredService<IUserAdmin>();

        // Removing an unknown user still runs the delete batch against storage.
        _ = await admin.TryRemoveAsync(UserSubjectId.New(), _ct);

        DataCategoryNameRecorder.UsedCategoriesShouldBe(serviceProvider, DataCategoryName.UserManagement);
    }

    [Fact]
    public async Task User_self_service_uses_correct_storage_category()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        var selfService = serviceProvider.GetRequiredService<IUserSelfService>();

        // Deleting an unknown user still runs the delete batch against storage.
        _ = await selfService.TryDeleteAsync(UserSubjectId.New(), _ct);

        DataCategoryNameRecorder.UsedCategoriesShouldBe(serviceProvider, DataCategoryName.UserManagement);
    }

    [Fact]
    public async Task Role_admin_uses_correct_storage_category()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        var admin = serviceProvider.GetRequiredService<IRoleAdmin>();

        var result = await admin.CreateAsync(new Role { Name = RoleName.Create($"role-{Guid.NewGuid():N}") }, _ct);

        result.IsSuccess.ShouldBeTrue();

        DataCategoryNameRecorder.UsedCategoriesShouldBe(serviceProvider, DataCategoryName.UserManagement);
    }

    [Fact]
    public async Task Group_admin_uses_correct_storage_category()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        var admin = serviceProvider.GetRequiredService<IGroupAdmin>();

        var result = await admin.CreateAsync(new Group { Name = UniqueName() }, _ct);

        result.IsSuccess.ShouldBeTrue();

        DataCategoryNameRecorder.UsedCategoriesShouldBe(serviceProvider, DataCategoryName.UserManagement);
    }

    [Fact]
    public async Task Membership_admin_uses_correct_storage_category()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        var groupId = (await serviceProvider.GetRequiredService<IGroupAdmin>().CreateAsync(new Group { Name = UniqueName() }, _ct)).Id!.Value;
        var membership = serviceProvider.GetRequiredService<IMembershipAdmin>();

        var result = await membership.AssignGroupAsync(UserSubjectId.New(), groupId, _ct);

        result.IsSuccess.ShouldBeTrue();

        DataCategoryNameRecorder.UsedCategoriesShouldBe(serviceProvider, DataCategoryName.UserManagement);
    }

    [Fact]
    public async Task Authenticators_admin_uses_correct_storage_category()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        var admin = serviceProvider.GetRequiredService<IUserAuthenticatorsAdmin>();

        var result = await admin.TryAddAsync(UserSubjectId.New(), [TestData.CreateOtpAddress()], [TestData.CreateExternalAuthenticatorAddress()], _ct);

        _ = result.ShouldNotBeNull();

        DataCategoryNameRecorder.UsedCategoriesShouldBe(serviceProvider, DataCategoryName.UserManagement);
    }

    [Fact]
    public async Task External_authenticator_uses_correct_storage_category()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        var authenticator = serviceProvider.GetRequiredService<IExternalAuthenticator>();

        var result = await authenticator.TryAuthenticateAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        _ = result.ShouldBeOfType<ExternalAuthenticationResult.Success>();

        DataCategoryNameRecorder.UsedCategoriesShouldBe(serviceProvider, DataCategoryName.UserManagement);
    }

    [Fact]
    public async Task Profile_admin_uses_correct_storage_category()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        var admin = serviceProvider.GetRequiredService<IUserProfileAdmin>();

        var result = await admin.TryAddAsync(UserSubjectId.New(), ValidatedAttributeValueCollection.Empty, _ct);

        _ = result.ShouldNotBeNull();

        // Profile data lives in the UserManagement category; the profile schema is resolved via ISchemaAdmin,
        // which Duende.Storage routes through the DynamicSchemas category.
        DataCategoryNameRecorder.UsedCategoriesShouldBe(serviceProvider, DataCategoryName.UserManagement, DataCategoryName.DynamicSchemas);
    }

    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private int _counter;
    private GroupName UniqueName() => GroupName.Create($"{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}");
}
