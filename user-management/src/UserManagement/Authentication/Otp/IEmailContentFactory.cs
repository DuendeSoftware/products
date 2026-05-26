// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

public interface IEmailContentFactory
{
    Task<EmailContent> CreateAsync(PlainTextOtp otp, TimeSpan expiresAfter, Ct ct);
}
