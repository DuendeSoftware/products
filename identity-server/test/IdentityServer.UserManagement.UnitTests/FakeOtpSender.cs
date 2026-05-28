// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Otp;

namespace Duende.UserManagement;

using Call = (OtpAddress Address, PlainTextOtp Otp, TimeSpan ExpiresAfter);

public sealed class FakeOtpSender : IOtpSender
{
    private readonly List<Call> _calls = [];

    public IReadOnlyCollection<Call> Calls => [.. _calls];

    public bool CanSend(OtpAddress address) => true;

    public Task SendAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct)
    {
        _calls.Add((address, otp, expiresAfter));
        return Task.CompletedTask;
    }

    public void ClearCalls() => _calls.Clear();
}
