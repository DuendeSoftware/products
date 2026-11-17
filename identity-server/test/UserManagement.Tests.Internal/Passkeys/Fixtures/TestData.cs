// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;

namespace Duende.Platform.UserManagement.Passkeys.Fixtures;

internal static class TestData
{
    private static int _counter;

    internal static OtpAddress CreateOtpAddress() =>
        new(OtpChannel.Email, EmailAddress.Create($"a{Count()}@b"));

    internal static ExternalAuthenticatorAddress CreateExternalAuthenticatorAddress() =>
        new(ExternalAuthenticatorName.Create("test"), OpaqueSubjectId.Create($"sub-{Count()}"));

    private static int Count() => Interlocked.Increment(ref _counter);
}
