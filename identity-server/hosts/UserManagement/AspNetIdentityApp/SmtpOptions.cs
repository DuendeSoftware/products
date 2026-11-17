// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace AspNetIdentityApp;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool EnableSsl { get; set; }
    public string FromEmail { get; set; } = "noreply@example.com";
    public string FromName { get; set; } = "Sample App";
}
