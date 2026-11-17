// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace AuthenticationFlowWithWebComponent.ServerComponent;

/// <summary>
/// Configuration options for the Persona Web Component
/// </summary>
public class WebComponentOptions
{
    /// <summary>
    /// Gets or sets the authentication configuration
    /// </summary>
    public AuthenticationConfiguration Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets the session configuration
    /// </summary>
    public SessionConfiguration Session { get; set; } = new();

    /// <summary>
    /// Gets or sets the OTP configuration
    /// </summary>
    public OtpConfiguration Otp { get; set; } = new();
}

/// <summary>
/// Authentication method configuration
/// </summary>
public class AuthenticationConfiguration
{
    /// <summary>
    /// Gets or sets whether OTP (email) authentication is enabled
    /// </summary>
    public bool IsOtpEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether external authentication is enabled
    /// </summary>
    public bool IsExternalAuthEnabled { get; set; }

    /// <summary>
    /// Gets the configured external authentication providers
    /// </summary>
    public List<ExternalProviderConfiguration> ExternalProviders { get; } = new();
}

/// <summary>
/// External authentication provider configuration
/// </summary>
public class ExternalProviderConfiguration
{
    /// <summary>
    /// Gets or sets the provider identifier (e.g., "google", "microsoft")
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the display name
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the challenge URL path
    /// </summary>
    public string ChallengeUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the authentication scheme name registered with AddAuthentication
    /// </summary>
    public string? AuthenticationScheme { get; init; }
}

/// <summary>
/// Session configuration
/// </summary>
public class SessionConfiguration
{
    /// <summary>
    /// Gets or sets whether "Remember Me" functionality is enabled
    /// </summary>
    public bool EnableRememberMe { get; set; } = true;
}

/// <summary>
/// OTP configuration
/// </summary>
public class OtpConfiguration
{
    /// <summary>
    /// Gets or sets the OTP length
    /// </summary>
    public int Length { get; set; } = 8;

    /// <summary>
    /// Gets or sets the OTP format (e.g., "XXXX-XXXX")
    /// </summary>
    public string Format { get; set; } = "XXXX-XXXX";

    /// <summary>
    /// Gets or sets the OTP expiry time in seconds
    /// </summary>
    public int ExpirySeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets whether OTP resend is allowed
    /// </summary>
    public bool AllowResend { get; set; } = true;

    /// <summary>
    /// Gets or sets the cooldown period between resends in seconds
    /// </summary>
    public int ResendCooldownSeconds { get; set; } = 60;
}
