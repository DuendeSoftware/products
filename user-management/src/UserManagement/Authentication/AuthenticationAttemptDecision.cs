// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication;

/// <summary>
/// The outcome of evaluating whether an authentication attempt should proceed.
/// </summary>
public abstract record AuthenticationAttemptDecision
{
    private AuthenticationAttemptDecision() { }

    /// <summary>
    /// Allow credential verification to proceed.
    /// </summary>
    public sealed record Allow : AuthenticationAttemptDecision;

    /// <summary>
    /// Reject the attempt before credential verification.
    /// </summary>
    public sealed record Reject : AuthenticationAttemptDecision;
}
