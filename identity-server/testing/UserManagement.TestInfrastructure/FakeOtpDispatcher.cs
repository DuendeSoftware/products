// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Otp;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Duende.UserManagement;
#pragma warning restore IDE0130

using Call = (OtpAddress Address, PlainTextOtp Otp, TimeSpan ExpiresAfter);

public sealed class FakeOtpDispatcher : IOtpDispatcher
{
    private readonly List<Call> _calls = [];

    public IReadOnlyCollection<Call> Calls => [.. _calls];

    public bool CanDispatch(OtpAddress address) => true;

    public Task DispatchAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct)
    {
        _calls.Add((address, otp, expiresAfter));
        return Task.CompletedTask;
    }

    public void ClearCalls() => _calls.Clear();
}
