// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace ExternalAuthentication;

public enum SignInError
{
    CouldNotAddAuthenticator,
    UserNotFoundForAddAuthenticator,
    CouldNotCreateUser,
    AuthenticatorAlreadyExists
}
