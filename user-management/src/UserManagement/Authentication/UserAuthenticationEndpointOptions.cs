// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal.Passkeys;

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Top-level options for configuring user authentication HTTP endpoints.
/// </summary>
public sealed class UserAuthenticationEndpointOptions
{
    /// <summary>
    /// Route options for passkey-related endpoints.
    /// </summary>
    public PasskeysRouteOptions Passkeys { get; init; } = new();
}

/// <summary>
/// Route configuration for passkey HTTP endpoints.
/// </summary>
public sealed class PasskeysRouteOptions
{
    /// <summary>
    /// The base route prefix for all passkey endpoints.
    /// Defaults to <c>/passkeys</c>.
    /// </summary>
    public string Route { get; set; } = PasskeyConstants.Urls.PasskeysRoute;

    /// <summary>
    /// The path for the passkey register/begin endpoint (relative to <see cref="Route"/>).
    /// Defaults to <c>/register/begin</c>.
    /// </summary>
    public string BeginRegistration { get; set; } = PasskeyConstants.Urls.BeginRegistration;

    /// <summary>
    /// The path for the passkey register/complete endpoint (relative to <see cref="Route"/>).
    /// Defaults to <c>/register/complete</c>.
    /// </summary>
    public string CompleteRegistration { get; set; } = PasskeyConstants.Urls.CompleteRegistration;

    /// <summary>
    /// The path for the passkey authenticate/begin endpoint (relative to <see cref="Route"/>).
    /// Defaults to <c>/authenticate/begin</c>.
    /// </summary>
    public string BeginAuthentication { get; set; } = PasskeyConstants.Urls.BeginAuthentication;

    /// <summary>
    /// The path for the passkey authenticate/discoverable/begin endpoint (relative to <see cref="Route"/>).
    /// Defaults to <c>/authenticate/discoverable/begin</c>.
    /// </summary>
    public string BeginDiscoverableAuthentication { get; set; } = PasskeyConstants.Urls.BeginDiscoverableAuthentication;

    /// <summary>
    /// The path for the passkey authenticate/complete endpoint (relative to <see cref="Route"/>).
    /// Defaults to <c>/authenticate/complete</c>.
    /// </summary>
    public string CompleteAuthentication { get; set; } = PasskeyConstants.Urls.CompleteAuthentication;

    /// <summary>
    /// The path for the passkeys JavaScript helper endpoint (relative to <see cref="Route"/>).
    /// Defaults to <c>/js</c>.
    /// </summary>
    public string PasskeysJavaScript { get; set; } = PasskeyConstants.Urls.PasskeysJs;
}
