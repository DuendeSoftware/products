// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace Duende.ConformanceReport;

/// <summary>
/// Options for the conformance assessment feature.
/// </summary>
public class ConformanceReportOptions
{
    /// <summary>
    /// Enable conformance endpoints. Requires valid license.
    /// Default: false
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Enable OAuth 2.1 conformance assessment.
    /// Default: true
    /// </summary>
    public bool EnableOAuth21Assessment { get; set; } = true;

    /// <summary>
    /// Enable FAPI 2.0 Security Profile conformance assessment.
    /// Default: true
    /// </summary>
    public bool EnableFapi2SecurityAssessment { get; set; } = true;

    /// <summary>
    /// Path prefix for conformance endpoints (without leading slash).
    /// Default: "_duende"
    /// </summary>
    public string PathPrefix { get; set; } = "_duende";

    /// <summary>
    /// ASP.NET Core authorization policy name for the HTML report endpoint.
    /// Default: "ConformanceReport"
    /// </summary>
    public string AuthorizationPolicyName { get; set; } = "ConformanceReport";

    /// <summary>
    /// Configures the authorization policy for the conformance report endpoint.
    /// By default, requires an authenticated user. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// If set to <c>null</c>, the authorization policy will NOT be automatically registered.
    /// In this case, you must manually register a policy with the name specified in 
    /// <see cref="AuthorizationPolicyName"/> (default: "ConformanceReport"), or the endpoint 
    /// will fail at runtime with a "policy not found" error.
    /// </para>
    /// <para>
    /// Setting to <c>null</c> is useful when you need to register the policy yourself with 
    /// custom logic that cannot be expressed through <see cref="AuthorizationPolicyBuilder"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// // Default behavior (requires authenticated user):
    /// options.ConfigureAuthorization = policy => policy.RequireAuthenticatedUser();
    /// 
    /// // Require specific role:
    /// options.ConfigureAuthorization = policy => policy.RequireRole("Admin");
    /// 
    /// // Require multiple claims:
    /// options.ConfigureAuthorization = policy => policy
    ///     .RequireRole("Admin")
    ///     .RequireClaim("department", "IT");
    /// 
    /// // Allow anonymous (not recommended for production):
    /// options.ConfigureAuthorization = policy => { };
    /// 
    /// // Manual policy registration (set to null and register policy yourself):
    /// options.ConfigureAuthorization = null;
    /// // Then in your Startup/Program.cs:
    /// // services.AddAuthorization(options => 
    /// //     options.AddPolicy("ConformanceReport", policy => 
    /// //         policy.Requirements.Add(new MyCustomRequirement())));
    /// </example>
    public Action<AuthorizationPolicyBuilder>? ConfigureAuthorization { get; set; }
        = policy => policy.RequireAuthenticatedUser();

    /// <summary>
    /// Optional display name of the host company to include in the report. This is for personalization and has no effect on the assessment results.
    /// </summary>
    public string? HostCompanyName { get; set; }

    /// <summary>
    /// Gets or sets the URL of the host company's logo.
    /// </summary>
    /// <remarks>Set this property to a valid URI that points to the logo image to display the host company's
    /// branding. If the value is null, no logo will be shown.</remarks>
    public Uri? HostCompanyLogoUrl { get; set; }
}
