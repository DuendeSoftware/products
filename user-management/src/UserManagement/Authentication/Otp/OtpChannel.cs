// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

[StringValue]
public partial record OtpChannel
{
    // Not that we really care about the exact number,
    // but we don't want people flooding the store with enormous values,
    // so we may as well use some reasonable number.
    private const int MaxLength = 255;

    public string Value { get; }

    public static OtpChannel Email { get; } = new("Email");

    public static OtpChannel Sms { get; } = new("SMS");

    static string Normalize(string value) => value.Trim();

    internal static OtpChannel Load(string value) => new(value);
}
