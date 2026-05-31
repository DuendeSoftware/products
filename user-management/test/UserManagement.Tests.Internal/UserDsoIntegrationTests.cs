// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class UserDsoIntegrationTests : IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly UserRepository _userRepository;
    private readonly IUserAuthenticatorsSelfService _authSelfService;
    private readonly IUserProfileSelfService _profileSelfService;
    private readonly IUserAdmin _userAdmin;
    private readonly IGroupAdmin _groupAdmin;
    private readonly IMembershipAdmin _membershipAdmin;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public UserDsoIntegrationTests()
    {
        _serviceProvider = UsersServiceProviderFactory.CreateAsync().GetAwaiter().GetResult();
        _userRepository = _serviceProvider.GetRequiredService<UserRepository>();
        _authSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _profileSelfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
        _userAdmin = _serviceProvider.GetRequiredService<IUserAdmin>();
        _groupAdmin = _serviceProvider.GetRequiredService<IGroupAdmin>();
        _membershipAdmin = _serviceProvider.GetRequiredService<IMembershipAdmin>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private static ExternalAuthenticator TestExternalAuthenticator() =>
        new(ExternalAuthenticatorName.Create("test"), OpaqueSubjectId.Create("sub-test"));

    [Fact]
    public async Task creating_authenticators_also_creates_user_dso()
    {
        var subjectId = UserSubjectId.New();
        _ = (await _authSelfService.TryRegisterAsync(subjectId, TestExternalAuthenticator(), _ct)).ShouldNotBeNull();

        var user = await _userRepository.TryReadAsync(subjectId, _ct);

        _ = user.ShouldNotBeNull();
        user!.Value.User.SubjectId.ShouldBe(subjectId.Value);
    }

    [Fact]
    public async Task creating_profile_also_creates_user_dso()
    {
        var subjectId = UserSubjectId.New();
        _ = (await _profileSelfService.TryRegisterAsync(subjectId, new AttributeValueCollection(AttributeSchema.Empty).Validate(), _ct)).ShouldNotBeNull();

        var user = await _userRepository.TryReadAsync(subjectId, _ct);

        _ = user.ShouldNotBeNull();
        user!.Value.User.SubjectId.ShouldBe(subjectId.Value);
    }

    [Fact]
    public async Task creating_authenticators_records_aspect_ref_in_user_dso()
    {
        var subjectId = UserSubjectId.New();
        _ = (await _authSelfService.TryRegisterAsync(subjectId, TestExternalAuthenticator(), _ct)).ShouldNotBeNull();

        var user = await _userRepository.TryReadAsync(subjectId, _ct);

        _ = user.ShouldNotBeNull();
        user!.Value.User.Aspects.ShouldContain(a => a.AspectEntityTypeId == 1000u);
    }

    [Fact]
    public async Task creating_profile_records_aspect_ref_in_user_dso()
    {
        var subjectId = UserSubjectId.New();
        _ = (await _profileSelfService.TryRegisterAsync(subjectId, new AttributeValueCollection(AttributeSchema.Empty).Validate(), _ct)).ShouldNotBeNull();

        var user = await _userRepository.TryReadAsync(subjectId, _ct);

        _ = user.ShouldNotBeNull();
        user!.Value.User.Aspects.ShouldContain(a => a.AspectEntityTypeId == 1500u);
    }

    [Fact]
    public async Task deleting_user_also_deletes_user_dso()
    {
        var subjectId = UserSubjectId.New();
        _ = (await _profileSelfService.TryRegisterAsync(subjectId, new AttributeValueCollection(AttributeSchema.Empty).Validate(), _ct)).ShouldNotBeNull();

        (await _userAdmin.TryRemoveAsync(subjectId, _ct)).ShouldBeTrue();

        var user = await _userRepository.TryReadAsync(subjectId, _ct);
        user.ShouldBeNull();
    }

    [Fact]
    public async Task assigning_group_creates_user_dso_if_not_exists()
    {
        var subjectId = UserSubjectId.New();
        var groupName = GroupName.Create($"group-{Guid.NewGuid():N}");
        var groupResult = await _groupAdmin.CreateAsync(new Group { Name = groupName }, _ct);
        groupResult.IsSuccess.ShouldBeTrue();

        (await _membershipAdmin.AssignGroupAsync(subjectId, groupResult.Id!.Value, _ct)).IsSuccess.ShouldBeTrue();

        var user = await _userRepository.TryReadAsync(subjectId, _ct);

        _ = user.ShouldNotBeNull();
        user!.Value.User.SubjectId.ShouldBe(subjectId.Value);
    }
}
