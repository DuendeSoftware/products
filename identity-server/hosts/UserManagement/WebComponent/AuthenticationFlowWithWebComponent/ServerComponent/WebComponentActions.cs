// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace AuthenticationFlowWithWebComponent.ServerComponent;

/// <summary>
/// Constants for next action for the component
/// </summary>
public static class WebComponentActions
{
    public const string Authenticated = "authenticated";
    public const string MfaRequired = "mfa-required";
    public const string OtpVerify = "otp-verify";
    public const string UnifiedForm = "unified-form";
    public const string Error = "error";
}
