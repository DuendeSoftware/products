// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class ExternalSignInsHowToTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IExternalAuthenticator _externalAuthenticator = null!;
    private IUserAuthenticatorsSelfService _selfService = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _externalAuthenticator = _serviceProvider.GetRequiredService<IExternalAuthenticator>();
        _selfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static ExternalAuthenticatorAddress CreateAddress(string providerName, string providerSubject)
    {
        // begin-snippet: external-link-address
        var address = new ExternalAuthenticatorAddress(
            ExternalAuthenticatorName.Create(providerName),
            OpaqueSubjectId.Create(providerSubject));
        // end-snippet
        return address;
    }
#pragma warning restore IDE0022

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static async Task<UserSubjectId?> SignInAsync(
        IExternalAuthenticator externalAuthenticator, ExternalAuthenticatorAddress address, Ct ct)
    {
        // begin-snippet: external-sign-in
        var result = await externalAuthenticator.TryAuthenticateAsync(address, ct);

        var subjectId = result switch
        {
            ExternalAuthenticationResult.Success success => success.UserSubjectId,
            ExternalAuthenticationResult.Failure => (UserSubjectId?)null,
            _ => throw new InvalidOperationException("Unexpected result type.")
        };
        // end-snippet
        return subjectId;
    }
#pragma warning restore IDE0022

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static async Task<bool> UnlinkAsync(
        IUserAuthenticatorsSelfService selfService, UserSubjectId subjectId, ExternalAuthenticatorAddress address, Ct ct)
    {
        // begin-snippet: external-unlink
        var removed = await selfService.TryRemoveExternalAuthenticatorAddressAsync(subjectId, address, ct);
        // end-snippet
        return removed;
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task First_sign_in_with_a_new_address_creates_a_user_and_links_it()
    {
        var address = CreateAddress("test-ext", Guid.NewGuid().ToString());

        var subjectId = await SignInAsync(_externalAuthenticator, address, _ct);

        _ = subjectId.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_linked_address_resolves_to_the_same_subject_on_every_sign_in()
    {
        var address = CreateAddress("test-ext", Guid.NewGuid().ToString());

        var firstSignIn = await SignInAsync(_externalAuthenticator, address, _ct);
        var secondSignIn = await SignInAsync(_externalAuthenticator, address, _ct);

        secondSignIn.ShouldBe(firstSignIn);
    }

    [Fact]
    public async Task Two_different_addresses_resolve_to_different_users()
    {
        var addressA = CreateAddress("test-ext", Guid.NewGuid().ToString());
        var addressB = CreateAddress("test-ext", Guid.NewGuid().ToString());

        var subjectA = await SignInAsync(_externalAuthenticator, addressA, _ct);
        var subjectB = await SignInAsync(_externalAuthenticator, addressB, _ct);

        subjectB.ShouldNotBe(subjectA);
    }

    [Fact]
    public async Task Unlinking_an_address_makes_the_next_sign_in_create_a_new_user()
    {
        var address = CreateAddress("test-ext", Guid.NewGuid().ToString());
        var subjectId = await SignInAsync(_externalAuthenticator, address, _ct);
        _ = subjectId.ShouldNotBeNull();

        var removed = await UnlinkAsync(_selfService, subjectId!.Value, address, _ct);
        removed.ShouldBeTrue();

        var resignedInSubjectId = await SignInAsync(_externalAuthenticator, address, _ct);

        resignedInSubjectId.ShouldNotBe(subjectId);
    }

    [Fact]
    public async Task Unlinking_a_users_only_authenticator_still_succeeds()
    {
        // Known gap: User Management doesn't stop a self-service caller from removing a user's last
        // authenticator, leaving the user with no way to sign in again. This test documents the actual
        // behavior, not an intended guarantee — see the follow-ups note.
        var address = CreateAddress("test-ext", Guid.NewGuid().ToString());
        var subjectId = await SignInAsync(_externalAuthenticator, address, _ct);
        _ = subjectId.ShouldNotBeNull();

        var removed = await UnlinkAsync(_selfService, subjectId!.Value, address, _ct);

        removed.ShouldBeTrue();
    }

    [Fact]
    public async Task Unlinking_an_address_the_user_never_had_still_reports_success()
    {
        // TryRemoveExternalAuthenticatorAddressAsync only checks that the user record exists; removing an
        // address that was never linked is a no-op against the authenticator set, so it still reports true.
        var address = CreateAddress("test-ext", Guid.NewGuid().ToString());
        var subjectId = await SignInAsync(_externalAuthenticator, address, _ct);
        _ = subjectId.ShouldNotBeNull();

        var neverLinked = CreateAddress("test-ext", Guid.NewGuid().ToString());
        var removed = await UnlinkAsync(_selfService, subjectId!.Value, neverLinked, _ct);

        removed.ShouldBeTrue();
    }
}
