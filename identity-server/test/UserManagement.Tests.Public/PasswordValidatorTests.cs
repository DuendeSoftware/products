// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class PasswordValidatorTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private ServiceProvider _serviceProvider = null!;
    private IUserAuthenticatorsSelfService _selfService = null!;
    private IExternalAuthenticator _externalAuthenticator = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _selfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _externalAuthenticator = _serviceProvider.GetRequiredService<IExternalAuthenticator>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task No_validators_registered_allows_password_to_be_set()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var password = await TestData.CreatePasswordAsync(_selfService, subjectId, _ct);

        var result = await _selfService.TrySetPasswordAsync(subjectId, password, _ct);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AcceptAll_validator_allows_set_password()
    {
        await using var sp = await CreateServiceProviderWithValidator<AcceptAllValidator>();
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var subjectId = await sp.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var password = await TestData.CreatePasswordAsync(selfService, subjectId, _ct);

        var result = await selfService.TrySetPasswordAsync(subjectId, password, _ct);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task RejectAll_validator_prevents_password_creation()
    {
        await using var sp = await CreateServiceProviderWithValidator<RejectAllValidator>();
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var result = await selfService.TryValidatePasswordAsync(UserSubjectId.New(), $"ABcd12!@{Guid.NewGuid()}", _ct);

        var failed = result.ShouldBeOfType<PasswordCreationResult.Failed>();
        failed.Errors.ShouldContain("All passwords rejected by test validator");
    }

    [Fact]
    public async Task Blocklist_validator_blocks_specific_password_and_allows_others()
    {
        await using var sp = await CreateServiceProviderWithValidator<BlocklistValidator>();
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var allowedSubjectId = await sp.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var allowedPassword = await TestData.CreatePasswordAsync(selfService, allowedSubjectId, _ct);

        var allowedResult = await selfService.TrySetPasswordAsync(allowedSubjectId, allowedPassword, _ct);
        allowedResult.ShouldBeTrue();
    }

    [Fact]
    public async Task Multiple_validators_any_rejection_prevents_password_creation()
    {
        await using var sp = await CreateServiceProviderWithTwoValidators<AcceptAllValidator, RejectAllValidator>();
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var result = await selfService.TryValidatePasswordAsync(UserSubjectId.New(), $"ABcd12!@{Guid.NewGuid()}", _ct);

        _ = result.ShouldBeOfType<PasswordCreationResult.Failed>();
    }

    [Fact]
    public async Task Multiple_accept_validators_allow_password()
    {
        await using var sp = await CreateServiceProviderWithTwoValidators<AcceptAllValidator, AcceptAllValidator>();
        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();
        var subjectId = await sp.GetRequiredService<IExternalAuthenticator>().CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var password = await TestData.CreatePasswordAsync(selfService, subjectId, _ct);

        var result = await selfService.TrySetPasswordAsync(subjectId, password, _ct);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task AddPasswordValidator_builder_extension_registers_validator()
    {
        await using var sp = await UsersServiceProviderFactory.CreateUsersBuilderAsync(
                configureOptions: null, addDataProtection: false,
                configureServices: services => _ = services.AddTransient<IPasswordValidator, RejectAllValidator>());

        var selfService = sp.GetRequiredService<IUserAuthenticatorsSelfService>();

        var result = await selfService.TryValidatePasswordAsync(UserSubjectId.New(), $"ABcd12!@{Guid.NewGuid()}", _ct);

        _ = result.ShouldBeOfType<PasswordCreationResult.Failed>();
    }

    private static async Task<ServiceProvider> CreateServiceProviderWithValidator<TValidator>()
        where TValidator : class, IPasswordValidator =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null, addDataProtection: false,
            configureServices: services => _ = services.AddTransient<IPasswordValidator, TValidator>());

    private static async Task<ServiceProvider> CreateServiceProviderWithTwoValidators<TValidator1, TValidator2>()
        where TValidator1 : class, IPasswordValidator
        where TValidator2 : class, IPasswordValidator =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null, addDataProtection: false,
            configureServices: services =>
            {
                _ = services.AddTransient<IPasswordValidator, TValidator1>();
                _ = services.AddTransient<IPasswordValidator, TValidator2>();
            });

    private sealed class AcceptAllValidator : IPasswordValidator
    {
        public Task<PasswordValidationResult> ValidateAsync(UserSubjectId userId, string password, Ct ct) =>
            Task.FromResult<PasswordValidationResult>(new PasswordValidationResult.Accepted());
    }

    private sealed class RejectAllValidator : IPasswordValidator
    {
        public Task<PasswordValidationResult> ValidateAsync(UserSubjectId userId, string password, Ct ct) =>
            Task.FromResult<PasswordValidationResult>(
                new PasswordValidationResult.Rejected("All passwords rejected by test validator"));
    }

    private sealed class BlocklistValidator : IPasswordValidator
    {
        private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "123456", "qwerty"
        };

        public Task<PasswordValidationResult> ValidateAsync(UserSubjectId userId, string password, Ct ct) =>
            Task.FromResult<PasswordValidationResult>(
                Blocklist.Contains(password)
                    ? new PasswordValidationResult.Rejected("Password appears in blocklist")
                    : new PasswordValidationResult.Accepted());
    }
}
