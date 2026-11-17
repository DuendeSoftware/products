// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace AuthenticationFlowWithWebComponent.ServerComponent;

/// <summary>
/// Standardized response for all authentication operations
/// </summary>
public record AuthenticationComponentResponse
{
    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The next action the client should take
    /// </summary>
    public string? NextAction { get; init; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Additional data specific to the authentication method
    /// </summary>
    public object? Data { get; init; }

    public static AuthenticationComponentResponse SuccessResult(string? nextAction = null, object? data = null) =>
        new()
        {
            Success = true,
            NextAction = nextAction ?? WebComponentActions.Authenticated,
            Data = data
        };

    public static AuthenticationComponentResponse RequiresMfa(object? data = null) =>
        new()
        {
            Success = true,
            NextAction = WebComponentActions.MfaRequired,
            Data = data
        };

    public static AuthenticationComponentResponse RequiresOtpVerification(object? data = null) =>
        new()
        {
            Success = true,
            NextAction = WebComponentActions.OtpVerify,
            Data = data
        };

    public static AuthenticationComponentResponse ErrorResult(string error) => new()
    {
        Success = false,
        NextAction = WebComponentActions.Error,
        Error = error
    };
}
