// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal.Passkeys;

internal static class PasskeyConstants
{
    internal static class Urls
    {
        public const string PasskeysRoute = "/passkeys";
        public const string PasskeysJs = "/js";
        public const string BeginRegistration = "/register/begin";
        public const string CompleteRegistration = "/register/complete";
        public const string BeginAuthentication = "/authenticate/begin";
        public const string BeginDiscoverableAuthentication = "/authenticate/discoverable/begin";
        public const string CompleteAuthentication = "/authenticate/complete";
    }
}
