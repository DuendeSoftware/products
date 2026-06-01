// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Pagination;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class UserSelfServicing : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IUserAuthenticatorsSelfService _authenticatorsSelfService = null!;
    private IExternalAuthenticator _externalAuthenticator = null!;
    private IGroupAdmin _groupAdmin = null!;
    private IMembershipAdmin _membershipAdmin = null!;
    private IUserSelfService _userSelfService = null!;
    private IUserProfileSelfService _profileSelfService = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _authenticatorsSelfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _externalAuthenticator = _serviceProvider.GetRequiredService<IExternalAuthenticator>();
        _groupAdmin = _serviceProvider.GetRequiredService<IGroupAdmin>();
        _membershipAdmin = _serviceProvider.GetRequiredService<IMembershipAdmin>();
        _profileSelfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
        _userSelfService = _serviceProvider.GetRequiredService<IUserSelfService>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_deregister()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var groupId = (await _groupAdmin.CreateAsync(new Group { Name = GroupName.Create("group1") }, _ct)).ShouldNotBeNull().Id.ShouldNotBeNull();
        (await _membershipAdmin.AssignGroupAsync(subjectId, groupId, _ct)).IsSuccess.ShouldBeTrue();
        _ = (await _profileSelfService.TryCreateAsync(subjectId, ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();

        var deregistered = await _userSelfService.TryDeleteAsync(subjectId, _ct);

        deregistered.ShouldBeTrue();
        (await _authenticatorsSelfService.TryGetAsync(subjectId, _ct)).ShouldBeNull();
        (await _membershipAdmin.GetMembersInGroupAsync(groupId, DataRange.FromPage(1), _ct)).Items.ShouldNotContain(item => item.SubjectId == subjectId);
        (await _profileSelfService.TryGetAsync(subjectId, _ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Can_deregister_idempotently()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        _ = (await _profileSelfService.TryCreateAsync(subjectId, ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();
        (await _userSelfService.TryDeleteAsync(subjectId, _ct)).ShouldBeTrue();

        var deregistered = await _userSelfService.TryDeleteAsync(subjectId, _ct);

        deregistered.ShouldBeTrue();
    }
}
