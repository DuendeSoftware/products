// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class PasswordCreation : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IUserAuthenticatorsSelfService _factory = null!;
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        _provider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            options.Passwords.MaxLength = 14;
            options.Passwords.MinDigits = 3;
            options.Passwords.MinLength = 13;
            options.Passwords.MinLower = 3;
            options.Passwords.MinSymbols = 3;
            options.Passwords.MinUpper = 3;
        });

        _factory = _provider.GetRequiredService<IUserAuthenticatorsSelfService>();
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    [Fact]
    public async Task Can_parse_passwords()
    {
        const string password = "abcDEF789`~!X";

        var exception = await Record.ExceptionAsync(async () => _ = await _factory.ValidatePasswordAsync(UserSubjectId.New(), password, _ct));

        exception.ShouldBeNull();
    }

    [Theory]
    [InlineData("> MaxLength", "abcDEF789`~!XYZ")]
    [InlineData("< MinDigits", "abcDEFG89`~!X")]
    [InlineData("< MinDigits (numeric Unicode)", "abcDEF⁷89`~!X")] // ⁷ is numeric Unicode, but not a digit
    [InlineData("< MinDigits (non-ASCII digit)", "abcDEF٧89`~!X")] // Arabic-Indic seven is a digit, but not ASCII (0-9)
    [InlineData("< MinLength", "abcDEF789`~!")]
    [InlineData("< MinLower", "abCDEF789`~!X")]
    [InlineData("< MinSymbols", "abcDEF7890~!X")]
    [InlineData("< MinUpper", "abcdEF789`~!x")]
    public async Task CannotParsePasswordsWhichDoNotMeetRequirements(string description, string password)
    {
        var exception = await Record.ExceptionAsync(async () => _ = await _factory.ValidatePasswordAsync(UserSubjectId.New(), password, _ct));

        _ = exception.ShouldBeOfType<FormatException>();
    }
}

public sealed class DefaultPasswordPolicy : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IUserAuthenticatorsSelfService _selfService = null!;
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        _provider = await UsersServiceProviderFactory.CreateAsync();
        _selfService = _provider.GetRequiredService<IUserAuthenticatorsSelfService>();
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    [Fact]
    public async Task Rejects_14_character_password()
    {
        var result = await _selfService.TryValidatePasswordAsync(UserSubjectId.New(), "correcthorsebt", _ct);

        _ = result.ShouldBeOfType<PasswordCreationResult.Failed>();
    }

    [Theory]
    [InlineData("lowercase only", "correcthorsebat")]
    [InlineData("uppercase only", "CORRECTHORSEBAT")]
    public async Task Accepts_15_character_password_without_composition_rules(string description, string password)
    {
        var result = await _selfService.TryValidatePasswordAsync(UserSubjectId.New(), password, _ct);

        _ = result.ShouldBeOfType<PasswordCreationResult.Success>(description);
    }
}
