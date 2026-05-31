// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class UserAuthenticatorsAdministration : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly List<ExternalAuthenticator> _externalAuthenticators = [.. TestData.SubjectIdTypes.Select(TestData.CreateExternalAuthenticator)];
    private readonly List<OtpAddress> _otpAddresses = [.. TestData.SubjectIdTypes.Select(TestData.CreateOtpAddress)];
    private IUserAuthenticatorsAdmin _authenticatorsAdmin = null!;
    private IUserAdmin _userAdmin = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _authenticatorsAdmin = _serviceProvider.GetRequiredService<IUserAuthenticatorsAdmin>();
        _userAdmin = _serviceProvider.GetRequiredService<IUserAdmin>();
        _externalAuthenticators.Count.ShouldBeGreaterThan(1);
        _otpAddresses.Count.ShouldBeGreaterThan(1);
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_add_user()
    {
        var user = await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), _otpAddresses, _externalAuthenticators, _ct);

        _ = user.ShouldNotBeNull();
        user.OtpAddresses.ShouldBe(_otpAddresses);
        user.ExternalAuthenticators.ShouldBe(_externalAuthenticators);
    }

    [Fact]
    public async Task Cannot_add_two_users_with_the_same_OtpAddresses()
    {
        _ = await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), _otpAddresses, [], _ct);

        var subjectId = await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), _otpAddresses, [], _ct);

        subjectId.ShouldBeNull();
    }

    [Fact]
    public async Task Cannot_add_two_users_with_the_same_ExternalAuthenticators()
    {
        _ = await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [], _externalAuthenticators, _ct);

        var subjectId = await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [], _externalAuthenticators, _ct);

        subjectId.ShouldBeNull();
    }

    [Fact]
    public async Task Can_get_user_by_SubjectId()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), _otpAddresses, [], _ct)).ShouldNotBeNull().SubjectId;

        var user = await _authenticatorsAdmin.TryGetAsync(subjectId, _ct);

        user.ShouldNotBeNull().OtpAddresses.ShouldBe(_otpAddresses);
    }

    [Fact]
    public async Task Cannot_get_removed_user()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), _otpAddresses, [], _ct)).ShouldNotBeNull().SubjectId;
        (await _userAdmin.TryRemoveAsync(subjectId, _ct)).ShouldBeTrue();

        var user = await _authenticatorsAdmin.TryGetAsync(subjectId, _ct);

        user.ShouldBeNull();
    }

    [Fact]
    public async Task Can_add_OtpAddress()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [_otpAddresses[0]], [], _ct)).ShouldNotBeNull().SubjectId;

        var added = await _authenticatorsAdmin.TryAddOtpAddressesAsync(subjectId, [_otpAddresses[1]], _ct);

        added.ShouldBeTrue();
        (await _authenticatorsAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().OtpAddresses.ShouldContain(_otpAddresses[1]);
    }

    [Fact]
    public async Task Can_add_OtpAddress_idempotently()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [_otpAddresses[0]], [], _ct)).ShouldNotBeNull().SubjectId;
        (await _authenticatorsAdmin.TryAddOtpAddressesAsync(subjectId, [_otpAddresses[1]], _ct)).ShouldBeTrue();

        var added = await _authenticatorsAdmin.TryAddOtpAddressesAsync(subjectId, [_otpAddresses[1]], _ct);

        added.ShouldBeTrue();
    }

    [Fact]
    public async Task Cannot_add_the_same_OtpAddress_to_two_users()
    {
        _ = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [_otpAddresses[0]], [], _ct)).ShouldNotBeNull();
        var subjectId2 = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [_otpAddresses[1]], [], _ct)).ShouldNotBeNull().SubjectId;

        var added = await _authenticatorsAdmin.TryAddOtpAddressesAsync(subjectId2, [_otpAddresses[0]], _ct);

        added.ShouldBeFalse();
    }

    [Fact]
    public async Task Cannot_add_OtpAddress_to_removed_user()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [_otpAddresses[0]], [], _ct)).ShouldNotBeNull().SubjectId;
        (await _userAdmin.TryRemoveAsync(subjectId, _ct)).ShouldBeTrue();

        var added = await _authenticatorsAdmin.TryAddOtpAddressesAsync(subjectId, [_otpAddresses[1]], _ct);

        added.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_remove_OtpAddress()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [_otpAddresses[0]], [], _ct)).ShouldNotBeNull().SubjectId;

        var removed = await _authenticatorsAdmin.TryRemoveOtpAddressesAsync(subjectId, [_otpAddresses[0]], _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().OtpAddresses.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_remove_OtpAddress_idempotently()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [_otpAddresses[0]], [], _ct)).ShouldNotBeNull().SubjectId;
        (await _authenticatorsAdmin.TryRemoveOtpAddressesAsync(subjectId, [_otpAddresses[0]], _ct)).ShouldBeTrue();

        var removed = await _authenticatorsAdmin.TryRemoveOtpAddressesAsync(subjectId, [_otpAddresses[0]], _ct);

        removed.ShouldBeTrue();
    }

    [Fact]
    public async Task Cannot_remove_OtpAddress_from_removed_user()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [_otpAddresses[0]], [], _ct)).ShouldNotBeNull().SubjectId;
        (await _userAdmin.TryRemoveAsync(subjectId, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsAdmin.TryRemoveOtpAddressesAsync(subjectId, [_otpAddresses[0]], _ct);

        removed.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_add_ExternalAuthenticator()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [], [_externalAuthenticators[0]], _ct)).ShouldNotBeNull().SubjectId;

        var added = await _authenticatorsAdmin.TryAddExternalAuthenticatorsAsync(subjectId, [_externalAuthenticators[1]], _ct);

        added.ShouldBeTrue();
        (await _authenticatorsAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().ExternalAuthenticators.ShouldContain(_externalAuthenticators[1]);
    }

    [Fact]
    public async Task Can_add_ExternalAuthenticator_idempotently()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [], [_externalAuthenticators[0]], _ct)).ShouldNotBeNull().SubjectId;
        (await _authenticatorsAdmin.TryAddExternalAuthenticatorsAsync(subjectId, [_externalAuthenticators[1]], _ct)).ShouldBeTrue();

        var added = await _authenticatorsAdmin.TryAddExternalAuthenticatorsAsync(subjectId, [_externalAuthenticators[1]], _ct);

        added.ShouldBeTrue();
    }

    [Fact]
    public async Task Cannot_add_the_same_ExternalAuthenticator_to_two_users()
    {
        _ = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [], [_externalAuthenticators[0]], _ct)).ShouldNotBeNull();
        var subjectId2 = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [], [_externalAuthenticators[1]], _ct)).ShouldNotBeNull().SubjectId;

        var added = await _authenticatorsAdmin.TryAddExternalAuthenticatorsAsync(subjectId2, [_externalAuthenticators[0]], _ct);

        added.ShouldBeFalse();
    }

    [Fact]
    public async Task Cannot_add_ExternalAuthenticator_to_removed_user()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [], [_externalAuthenticators[0]], _ct)).ShouldNotBeNull().SubjectId;
        (await _userAdmin.TryRemoveAsync(subjectId, _ct)).ShouldBeTrue();

        var added = await _authenticatorsAdmin.TryAddExternalAuthenticatorsAsync(subjectId, [_externalAuthenticators[1]], _ct);

        added.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_remove_ExternalAuthenticator()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [], [_externalAuthenticators[0]], _ct)).ShouldNotBeNull().SubjectId;

        var removed = await _authenticatorsAdmin.TryRemoveExternalAuthenticatorsAsync(subjectId, [_externalAuthenticators[0]], _ct);

        removed.ShouldBeTrue();
        (await _authenticatorsAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull().ExternalAuthenticators.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_remove_ExternalAuthenticator_idempotently()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [], [_externalAuthenticators[0]], _ct)).ShouldNotBeNull().SubjectId;
        (await _authenticatorsAdmin.TryRemoveExternalAuthenticatorsAsync(subjectId, [_externalAuthenticators[0]], _ct)).ShouldBeTrue();

        var removed = await _authenticatorsAdmin.TryRemoveExternalAuthenticatorsAsync(subjectId, [_externalAuthenticators[0]], _ct);

        removed.ShouldBeTrue();
    }

    [Fact]
    public async Task Cannot_remove_ExternalAuthenticator_from_removed_user()
    {
        var subjectId = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), [], [_externalAuthenticators[0]], _ct)).ShouldNotBeNull().SubjectId;
        (await _userAdmin.TryRemoveAsync(subjectId, _ct)).ShouldBeTrue();

        var removed = await _authenticatorsAdmin.TryRemoveExternalAuthenticatorsAsync(subjectId, [_externalAuthenticators[0]], _ct);

        removed.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_query_all_authenticators()
    {
        var subjectId1 = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), _otpAddresses, [], _ct)).ShouldNotBeNull().SubjectId;
        var otpAddresses2 = new List<OtpAddress> { TestData.CreateOtpAddress() };
        var subjectId2 = (await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), otpAddresses2, [], _ct)).ShouldNotBeNull().SubjectId;

        var result = await _authenticatorsAdmin.QueryAsync(QueryRequest.Create(), _ct);

        result.Items.Count.ShouldBeGreaterThanOrEqualTo(2);
        result.Items.ShouldContain(u => u.SubjectId == subjectId1);
        result.Items.ShouldContain(u => u.SubjectId == subjectId2);
    }

    [Fact]
    public async Task QueryAsync_rejects_filter()
    {
        var request = QueryRequest.Create(FilterBy.FromSearchExpression(SearchExpression.Create("userName eq \"alice\"")));

        _ = await Should.ThrowAsync<NotSupportedException>(async () =>
            await _authenticatorsAdmin.QueryAsync(request, _ct));
    }

    [Fact]
    public async Task QueryAsync_rejects_sort()
    {
        var request = QueryRequest.Create(SortBy.Attribute(AttributeCode.Create("user_name")));

        _ = await Should.ThrowAsync<NotSupportedException>(async () =>
            await _authenticatorsAdmin.QueryAsync(request, _ct));
    }

    [Fact]
    public async Task QueryAsync_rejects_continuation_token_range()
    {
        var request = QueryRequest.Create(DataRange.FromContinuationToken(token: null, size: 10));

        _ = await Should.ThrowAsync<NotSupportedException>(async () =>
            await _authenticatorsAdmin.QueryAsync(request, _ct));
    }

    [Fact]
    public async Task QueryAsync_with_default_range_returns_default_page()
    {
        for (var i = 0; i < 30; i++)
        {
            var otpAddresses = new List<OtpAddress> { TestData.CreateOtpAddress() };
            _ = await _authenticatorsAdmin.TryAddAsync(UserSubjectId.New(), otpAddresses, [], _ct);
        }

        var result = await _authenticatorsAdmin.QueryAsync(QueryRequest.Create(), _ct);

        result.Items.Count.ShouldBeLessThanOrEqualTo(DataRangeSize.Default.Value);
    }
}
