// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace TotpAuthentication;

public sealed record OtpData(string Email, string Token, DateTimeOffset ExpiresAtUtc, string? ReturnUrl);

