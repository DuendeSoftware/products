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

public sealed class UserAdministration : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IUserAuthenticatorsAdmin _authenticatorsAdmin = null!;
    private IGroupAdmin _groupAdmin = null!;
    private IMembershipAdmin _membershipAdmin = null!;
    private IUserAdmin _userAdmin = null!;
    private IUserProfileAdmin _profileAdmin = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _authenticatorsAdmin = _serviceProvider.GetRequiredService<IUserAuthenticatorsAdmin>();
        _groupAdmin = _serviceProvider.GetRequiredService<IGroupAdmin>();
        _membershipAdmin = _serviceProvider.GetRequiredService<IMembershipAdmin>();
        _profileAdmin = _serviceProvider.GetRequiredService<IUserProfileAdmin>();
        _userAdmin = _serviceProvider.GetRequiredService<IUserAdmin>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_set_UserName()
    {
        var otpAddress = TestData.CreateOtpAddress();
        var userName = TestData.CreateUserName();
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [otpAddress], [], _ct)).ShouldNotBeNull().SubjectId;
        _ = (await _profileAdmin.TryAddAsync(subjectId, ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();

        var result = await _userAdmin.TrySetUserNameAsync(subjectId, userName, _ct);

        result.ShouldBeTrue();
        (await _authenticatorsAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().UserName.ShouldBe(userName);
        (await _profileAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().UserName.ShouldBe(userName);
    }

    [Fact]
    public async Task Can_remove_UserName()
    {
        var otpAddress = TestData.CreateOtpAddress();
        var userName = TestData.CreateUserName();
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [otpAddress], [], _ct)).ShouldNotBeNull().SubjectId;
        _ = (await _profileAdmin.TryAddAsync(subjectId, ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();
        (await _userAdmin.TrySetUserNameAsync(subjectId, userName, _ct)).ShouldBeTrue();

        var result = await _userAdmin.TryRemoveUserNameAsync(subjectId, _ct);

        result.ShouldBeTrue();
        (await _authenticatorsAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().UserName.ShouldBeNull();
        (await _profileAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().UserName.ShouldBeNull();
    }

    [Fact]
    public async Task Can_remove_user()
    {
        var user = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [TestData.CreateOtpAddress()], [], _ct)).ShouldNotBeNull();
        var groupId = (await _groupAdmin.CreateAsync(new Group { Name = GroupName.Create("group1") }, _ct)).ShouldNotBeNull().Id.ShouldNotBeNull();
        (await _membershipAdmin.AssignGroupAsync(user.SubjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        _ = (await _profileAdmin.TryAddAsync(user.SubjectId, ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();

        var removed = await _userAdmin.TryRemoveAsync(user.SubjectId, _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsAdmin.TryGetAsync(user.SubjectId, _ct)).ShouldBeNull();
        (await _membershipAdmin.GetMembersInGroupAsync(groupId, DataRange.FromPage(1), _ct)).Items.ShouldNotContain(item => item.SubjectId == user.SubjectId);
        (await _profileAdmin.TryGetAsync(user.SubjectId, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Can_remove_user_idempotently()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [TestData.CreateOtpAddress()], [], _ct)).ShouldNotBeNull().SubjectId;
        _ = (await _profileAdmin.TryAddAsync(subjectId, ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();
        (await _userAdmin.TryRemoveAsync(subjectId, _ct)).ShouldBeTrue();

        var removed = await _userAdmin.TryRemoveAsync(subjectId, _ct);

        removed.ShouldBeTrue();
    }
}
