// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class RecoveryCodeAuthentication : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly PlainTextRecoveryCode _incorrectCode;
    private IRecoveryCodeAuthenticator _auth = null!;
    private IUserAuthenticatorsSelfService _selfService = null!;
    private IExternalAuthenticator _externalAuthenticator = null!;
    private ServiceProvider _serviceProvider = null!;

    public RecoveryCodeAuthentication()
    {
        PlainTextRecoveryCode.TryCreate("123456-abcdefg", out var code).ShouldBeTrue();
        _incorrectCode = code!.Value;
    }

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            // Recovery codes generate 10 codes; authenticating all in sequence exceeds the default velocity threshold
            options.Throttling.MaxAttemptsPerWindow = 20;
        });
        _auth = _serviceProvider.GetRequiredService<IRecoveryCodeAuthenticator>();
        _selfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _externalAuthenticator = _serviceProvider.GetRequiredService<IExternalAuthenticator>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_authenticate_with_all_codes()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var codes = (await _selfService.TryCreateRecoveryCodesAsync(subjectId, _ct)).ShouldNotBeNull();

        for (var i = 0; i < codes.Count; i++)
        {
            var authenticated = await _auth.TryAuthenticateAsync(subjectId, codes.ElementAt(i), _ct);

            authenticated.ShouldBeTrue($"Could not authenticate with code {i}");
            var user = (await _selfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
            user.RecoveryCodeCount.ShouldBe(codes.Count - i - 1);
        }
    }

    [Fact]
    public async Task Can_authenticate_with_all_codes_in_reverse_order()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var codes = (await _selfService.TryCreateRecoveryCodesAsync(subjectId, _ct)).ShouldNotBeNull();

        for (var i = codes.Count - 1; i >= 0; i--)
        {
            var authenticated = await _auth.TryAuthenticateAsync(subjectId, codes.ElementAt(i), _ct);

            authenticated.ShouldBeTrue();
            var user = (await _selfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
            user.RecoveryCodeCount.ShouldBe(i);
        }
    }

    [Fact]
    public async Task Cannot_authenticate_with_the_same_code_twice()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var codes = (await _selfService.TryCreateRecoveryCodesAsync(subjectId, _ct)).ShouldNotBeNull();
        (await _auth.TryAuthenticateAsync(subjectId, codes.ElementAt(0), _ct)).ShouldBeTrue();

        var authenticatedAgain = await _auth.TryAuthenticateAsync(subjectId, codes.ElementAt(0), _ct);

        authenticatedAgain.ShouldBeFalse();
    }

    [Fact]
    public async Task Cannot_authenticate_with_an_incorrect_code()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        _ = (await _selfService.TryCreateRecoveryCodesAsync(subjectId, _ct)).ShouldNotBeNull();

        var authenticated = await _auth.TryAuthenticateAsync(subjectId, _incorrectCode, _ct);

        authenticated.ShouldBeFalse();
    }

    [Fact]
    public async Task Default_count_produces_10_codes()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var codes = (await _selfService.TryCreateRecoveryCodesAsync(subjectId, _ct)).ShouldNotBeNull();

        codes.Count.ShouldBe(10);
    }

    [Fact]
    public async Task Custom_count_produces_correct_number_of_codes()
    {
        await using var sp = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.RecoveryCodes.Count = 5;
            options.Throttling.MaxAttemptsPerWindow = 20;
        });
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var subjectId = await sp.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var codes = (await selfService.TryCreateRecoveryCodesAsync(subjectId, _ct)).ShouldNotBeNull();

        codes.Count.ShouldBe(5);
    }

    [Fact]
    public async Task Disabled_recovery_codes_cannot_be_generated()
    {
        await using var sp = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.RecoveryCodes.Enabled = false;
        });
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var subjectId = await sp.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var codes = await selfService.TryCreateRecoveryCodesAsync(subjectId, _ct);

        codes.ShouldBeNull();
    }

    [Fact]
    public async Task Disabled_recovery_codes_reject_authentication()
    {
        // Use a shared dbId so both service providers operate on the same in-memory store
        var sharedDbId = Guid.NewGuid();

        // First generate codes while enabled
        await using var spEnabled = await UsersServiceProviderFactory.CreateUsersBuilderAsync(options =>
        {
            options.Throttling.MaxAttemptsPerWindow = 20;
        }, false, dbId: sharedDbId);
        var selfServiceEnabled = spEnabled.GetRequiredService<IUserAuthenticatorsSelfService>();
        var subjectId = await spEnabled.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var codes = (await selfServiceEnabled.TryCreateRecoveryCodesAsync(subjectId, _ct)).ShouldNotBeNull();

        // Now use a service provider with recovery codes disabled but the same backing store — auth should be rejected
        await using var spDisabled = await UsersServiceProviderFactory.CreateUsersBuilderAsync(options =>
        {
            options.RecoveryCodes.Enabled = false;
        }, false, dbId: sharedDbId);
        var auth = spDisabled.GetRequiredService<IRecoveryCodeAuthenticator>();

        var authenticated = await auth.TryAuthenticateAsync(subjectId, codes.ElementAt(0), _ct);

        authenticated.ShouldBeFalse();
    }
}
