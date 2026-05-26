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
    private readonly IUserSelfService _userSelfService;

    public UserDsoIntegrationTests()
    {
        _serviceProvider = UsersServiceProviderFactory.CreateAsync().GetAwaiter().GetResult();
        _userRepository = _serviceProvider.GetRequiredService<UserRepository>();
        _authSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _userSelfService = _serviceProvider.GetRequiredService<IUserSelfService>();
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
    public async Task setting_username_updates_user_dso()
    {
        var subjectId = UserSubjectId.New();
        _ = (await _profileSelfService.TryRegisterAsync(subjectId, new AttributeValueCollection(AttributeSchema.Empty).Validate(), _ct)).ShouldNotBeNull();
        var userName = UserName.Create("testuser");

        (await _userAdmin.TrySetUserNameAsync(subjectId, userName, _ct)).ShouldBeTrue();

        var user = await _userRepository.TryReadAsync(subjectId, _ct);
        _ = user.ShouldNotBeNull();
        user!.Value.User.UserName.ShouldBe("testuser");
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

    [Fact]
    public async Task when_creating_profile_then_authenticator_inherits_username()
    {
        var subjectId = UserSubjectId.New();

        // create a new profile for subject id
        _ = (await _profileSelfService.TryRegisterAsync(subjectId, new AttributeValueCollection(AttributeSchema.Empty).Validate(), _ct)).ShouldNotBeNull();

        // set the username to "abc"
        (await _userSelfService.TrySetUserNameAsync(subjectId, "abc", _ct)).ShouldBeTrue();

        // create an authenticator for the same subject
        _ = (await _authSelfService.TryRegisterAsync(subjectId, TestExternalAuthenticator(), _ct)).ShouldNotBeNull();

        // the username on the authenticator should be "abc"
        var auth = await _authSelfService.TryGetAsync(subjectId, _ct);
        _ = auth.ShouldNotBeNull();
        auth.UserName.ShouldBe("abc");
    }

    [Fact]
    public async Task when_creating_authenticator_then_profile_inherits_username()
    {
        var subjectId = UserSubjectId.New();

        // create a new authenticator for subject
        _ = await _authSelfService.TryRegisterAsync(subjectId, TestExternalAuthenticator(), _ct);

        // set the username to "abc"
        (await _userSelfService.TrySetUserNameAsync(subjectId, "abc", _ct)).ShouldBeTrue();

        // create a profile for the same subject
        _ = await _profileSelfService.TryRegisterAsync(subjectId, new AttributeValueCollection(AttributeSchema.Empty).Validate(), _ct);

        // the username on the profile should be "abc"
        var profile = await _profileSelfService.TryGetAsync(subjectId, _ct);
        _ = profile.ShouldNotBeNull();
        profile.UserName.ShouldBe("abc");

    }

    [Fact]
    public async Task creating_authenticator_with_username_propagates_to_existing_user_dso()
    {
        var subjectId = UserSubjectId.New();

        // create a profile first (which creates the UserDso)
        _ = (await _profileSelfService.TryRegisterAsync(subjectId, new AttributeValueCollection(AttributeSchema.Empty).Validate(), _ct)).ShouldNotBeNull();

        // set a username on the user
        (await _userSelfService.TrySetUserNameAsync(subjectId, "original", _ct)).ShouldBeTrue();

        // now create authenticators — the username on UserDso should remain "original"
        _ = (await _authSelfService.TryRegisterAsync(subjectId, TestExternalAuthenticator(), _ct)).ShouldNotBeNull();

        var user = await _userRepository.TryReadAsync(subjectId, _ct);
        _ = user.ShouldNotBeNull();
        user!.Value.User.UserName.ShouldBe("original");
    }
}

public class UserTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private ServiceProvider _serviceProvider = null!;
    private IUserSelfService _userSelfService = null!;
    private IUserProfileSelfService _userProfileSelfService = null!;
    private IUserAuthenticatorsAdmin _userAuthenticatorsAdmin = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _userSelfService = _serviceProvider.GetRequiredService<IUserSelfService>();
        _userProfileSelfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
        _userAuthenticatorsAdmin = _serviceProvider.GetRequiredService<IUserAuthenticatorsAdmin>();
    }
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task Cannot_create_two_users_with_same_username()
    {
        var u1 = UserSubjectId.New();
        _ = await _userProfileSelfService.TryRegisterAsync(u1, new AttributeValueCollection(AttributeSchema.Empty).Validate(), _ct);
        var u2 = UserSubjectId.New();
        _ = await _userAuthenticatorsAdmin.TryAddAsync(u2, [], [TestExternalAuthenticator()], _ct);

        (await _userSelfService.TrySetUserNameAsync(u1, "bob", _ct)).ShouldBeTrue();
        (await _userSelfService.TrySetUserNameAsync(u2, "bob", _ct)).ShouldBeFalse();
    }

    private static ExternalAuthenticator TestExternalAuthenticator() =>
        new(ExternalAuthenticatorName.Create("test"), OpaqueSubjectId.Create("sub-test"));
}
