// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Represents the outcome of validating an id_token_hint's claims (sub/sid) against the
/// current user session during an end session (logout) request.
/// </summary>
public enum EndSessionHintValidationOutcome
{
    /// <summary>
    /// The id_token_hint's claims match the current session. Proceed with logout.
    /// </summary>
    Valid,

    /// <summary>
    /// The id_token_hint's claims do not match the current session. Reject the logout request.
    /// </summary>
    Invalid,

    /// <summary>
    /// The session match is uncertain. Proceed with logout but require the user to confirm.
    /// The logout UI will show a confirmation prompt (<see cref="Duende.IdentityServer.Models.LogoutRequest.ShowSignoutPrompt"/> will be <c>true</c>).
    /// <para>
    /// Note: <see cref="RequiresConfirmation"/> is advisory — the logout UI must respect
    /// <see cref="Duende.IdentityServer.Models.LogoutRequest.ShowSignoutPrompt"/> for the confirmation to be enforced.
    /// Custom logout UI implementations must check this property to meet the OIDC spec's
    /// requirement to prompt the user when the id_token_hint does not match the current session.
    /// </para>
    /// </summary>
    RequiresConfirmation
}

/// <summary>
/// Represents the result of validating an id_token_hint's claims against the current user
/// session during an end session (logout) request.
/// </summary>
/// <remarks>
/// Use the static factory methods <see cref="Valid"/>, <see cref="Invalid"/>, and
/// <see cref="RequiresConfirmation"/> to create instances.
/// <para>
/// <b>Security note</b>: Returning <see cref="Valid"/> unconditionally from a custom override of
/// <c>ValidateIdTokenHintAsync</c> (i.e., accepting any id_token_hint regardless of sub/sid match)
/// creates a cross-user logout vector. An attacker holding any valid id_token_hint can silently log
/// out other users when the signout prompt is suppressed. Ensure custom overrides apply appropriate
/// validation logic.
/// </para>
/// </remarks>
public sealed class EndSessionHintValidationResult
{
    /// <summary>
    /// Gets the outcome of the validation.
    /// </summary>
    public EndSessionHintValidationOutcome Outcome { get; }

    /// <summary>
    /// Gets the error message when <see cref="Outcome"/> is <see cref="EndSessionHintValidationOutcome.Invalid"/>.
    /// </summary>
    public string? ErrorMessage { get; }

    private EndSessionHintValidationResult(EndSessionHintValidationOutcome outcome, string? errorMessage = null)
    {
        Outcome = outcome;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a result indicating the id_token_hint's claims match the current session.
    /// </summary>
    public static EndSessionHintValidationResult Valid() =>
        new EndSessionHintValidationResult(EndSessionHintValidationOutcome.Valid);

    /// <summary>
    /// Creates a result indicating the id_token_hint's claims do not match the current session.
    /// The logout request will be rejected with the specified error message.
    /// </summary>
    /// <param name="errorMessage">A description of why validation failed.</param>
    public static EndSessionHintValidationResult Invalid(string errorMessage) =>
        new EndSessionHintValidationResult(EndSessionHintValidationOutcome.Invalid, errorMessage);

    /// <summary>
    /// Creates a result indicating that the session match is uncertain and the user should be
    /// prompted to confirm logout. The logout request proceeds but
    /// <see cref="ValidatedEndSessionRequest.RequiresConfirmation"/> will be set to <c>true</c>.
    /// </summary>
    public static EndSessionHintValidationResult RequiresConfirmation() =>
        new EndSessionHintValidationResult(EndSessionHintValidationOutcome.RequiresConfirmation);
}
