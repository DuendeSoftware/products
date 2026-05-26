// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

public abstract record OtpAuthenticationResult
{
    private OtpAuthenticationResult() { }

    public sealed record Success : OtpAuthenticationResult
    {
        internal Success(OtpAddress address, UserSubjectId userSubjectId)
        {
            Address = address;
            UserSubjectId = userSubjectId;
        }

        public OtpAddress Address { get; }

        public UserSubjectId UserSubjectId { get; }
    }

    public sealed record Failure : OtpAuthenticationResult
    {
        internal static Failure Instance { get; } = new();

        private Failure() { }
    }
}
