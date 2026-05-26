// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Evaluates whether an authentication attempt should proceed for a specific authenticator.
/// </summary>
public interface IAuthenticationAttemptPolicy
{
    /// <summary>
    /// Called before credential verification. Return <see cref="AuthenticationAttemptDecision.Allow"/> to proceed
    /// or <see cref="AuthenticationAttemptDecision.Reject"/> to short-circuit.
    /// </summary>
    /// <remarks>
    /// Implementations must not create observable differences between existing-account and
    /// non-existing-account failures. Avoid existence-dependent timing, messaging, or other
    /// caller-visible side effects.
    ///
    /// Implementations are also expected not to add measurable latency to authentication flows.
    /// The call should be effectively timing-neutral from the caller's perspective.
    /// </remarks>
    /// <param name="context">The authenticator and attempt-state context for the evaluation.</param>
    /// <param name="ct">A cancellation token for the asynchronous operation.</param>
    /// <returns>
    /// An <see cref="AuthenticationAttemptDecision"/> indicating whether credential verification
    /// should proceed.
    /// </returns>
    Task<AuthenticationAttemptDecision> EvaluateAsync(AuthenticationAttemptContext context, Ct ct);
}
