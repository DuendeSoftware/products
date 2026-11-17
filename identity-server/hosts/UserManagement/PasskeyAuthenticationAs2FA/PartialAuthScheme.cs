// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace PasskeyAuthenticationAs2FA;

internal static class PartialAuthScheme
{
    internal const string Name = "Identity.Partial";
    internal const string SecondFactorCompleteClaimType = "passkey_2fa_complete";
}
