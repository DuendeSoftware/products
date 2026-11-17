// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace OtpIdentityServer;

public sealed record OtpData(
    string Email, string Token, DateTimeOffset ExpiresAtUtc, string? ReturnUrl, string? Prompt = null);
