// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Pagination;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class UserSelfServicing : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IUserAuthenticatorsSelfService _authenticatorsSelfService = null!;
    private IGroupAdmin _groupAdmin = null!;
    private IMembershipAdmin _membershipAdmin = null!;
    private IUserSelfService _userSelfService = null!;
    private IUserProfileAdmin _profileAdmin = null!;
    private IUserProfileSelfService _profileSelfService = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _authenticatorsSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _groupAdmin = _serviceProvider.GetRequiredService<IGroupAdmin>();
        _membershipAdmin = _serviceProvider.GetRequiredService<IMembershipAdmin>();
        _profileAdmin = _serviceProvider.GetRequiredService<IUserProfileAdmin>();
        _profileSelfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
        _userSelfService = _serviceProvider.GetRequiredService<IUserSelfService>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_set_UserName()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        _ = (await _profileAdmin.TryAddAsync(user.SubjectId, ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();
        var userName = TestData.CreateUserName();

        var result = await _userSelfService.TrySetUserNameAsync(user.SubjectId, userName, _ct);

        result.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().UserName.ShouldBe(userName);
        (await _profileAdmin.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().UserName.ShouldBe(userName);
    }

    [Fact]
    public async Task Can_remove_UserName()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        _ = (await _profileAdmin.TryAddAsync(user.SubjectId, ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();
        (await _userSelfService.TrySetUserNameAsync(user.SubjectId, TestData.CreateUserName(), _ct)).ShouldBeTrue();

        var result = await _userSelfService.TryRemoveUserNameAsync(user.SubjectId, _ct);

        result.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().UserName.ShouldBeNull();
        (await _profileAdmin.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull().UserName.ShouldBeNull();
    }

    [Fact]
    public async Task Can_deregister()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        var groupId = (await _groupAdmin.CreateAsync(new Group { Name = GroupName.Create("group1") }, _ct)).ShouldNotBeNull().Id.ShouldNotBeNull();
        (await _membershipAdmin.AssignGroupAsync(user.SubjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        _ = (await _profileSelfService.TryRegisterAsync(user.SubjectId, ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();

        var deregistered = await _userSelfService.TryDeregisterAsync(user.SubjectId, _ct);

        deregistered.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldBeNull();
        (await _membershipAdmin.GetMembersInGroupAsync(groupId, DataRange.FromPage(1), _ct)).Items.ShouldNotContain(item => item.SubjectId == user.SubjectId);
        (await _profileSelfService.TryGetAsync(user.SubjectId, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Can_deregister_idempotently()
    {
        var user = (await _authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct)).ShouldNotBeNull();
        _ = (await _profileSelfService.TryRegisterAsync(user.SubjectId, ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();
        (await _userSelfService.TryDeregisterAsync(user.SubjectId, _ct)).ShouldBeTrue();

        var deregistered = await _userSelfService.TryDeregisterAsync(user.SubjectId, _ct);

        deregistered.ShouldBeTrue();
    }
}
