// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Platform.UserManagement;

public sealed class UserAuthenticationOptionsValidatorTests
{
    [Fact]
    public void invalid_passkey_challenge_size_fails_startup_validation()
    {
        var invalidChallengeSize = WebAuthnConstants.MinimumChallengeSize - 1;

        var services = new ServiceCollection();
        _ = services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users =>
                users.Authentication(authentication =>
                    authentication.Configure(options => options.Passkeys.ChallengeSize = invalidChallengeSize)));

        using var serviceProvider = services.BuildServiceProvider();

        var exception = Should.Throw<OptionsValidationException>(
            () => serviceProvider.GetRequiredService<IStartupValidator>().Validate());

        exception.Message.ShouldBe(
            $"Passkeys.ChallengeSize must be >= {WebAuthnConstants.MinimumChallengeSize}, but was {invalidChallengeSize}.");
    }
}
