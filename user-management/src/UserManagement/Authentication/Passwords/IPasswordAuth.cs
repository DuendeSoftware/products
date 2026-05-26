// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passwords;

public interface IPasswordAuth
{
    Task<PasswordAuthenticationResult> TryAuthenticateAsync(UserName userName, NonValidatedPassword password, Ct ct);
}
