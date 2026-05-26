// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

public sealed record EmailContent
{
    internal EmailContent(string subject, string body, bool isBodyHtml)
    {
        Subject = subject;
        Body = body;
        IsBodyHtml = isBodyHtml;
    }

    public string Subject { get; }
    public string Body { get; }
    public bool IsBodyHtml { get; }

    public void Deconstruct(out string subject, out string body, out bool isBodyHtml)
    {
        subject = Subject;
        body = Body;
        isBodyHtml = IsBodyHtml;
    }
}
