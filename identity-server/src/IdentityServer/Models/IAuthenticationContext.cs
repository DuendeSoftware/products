// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Protocol-agnostic context for an authentication request, providing
/// the common information needed by login/consent UI pages.
/// </summary>
public interface IAuthenticationContext
{
    /// <summary>
    /// The application that initiated the authentication request.
    /// </summary>
    IConnectedApplication Application { get; }

    /// <summary>
    /// The external identity provider requested. Used to bypass home realm
    /// discovery (HRD).
    /// </summary>
    string? IdP { get; }

    /// <summary>
    /// The expected username the user will use to login.
    /// </summary>
    string? LoginHint { get; }

    /// <summary>
    /// The tenant requested.
    /// </summary>
    string? Tenant { get; }

    /// <summary>
    /// The prompt modes requested (e.g. "login", "none"). Maps to OIDC
    /// prompt parameter values and SAML ForceAuthn/IsPassive flags.
    /// </summary>
    IEnumerable<string> PromptModes { get; }
}
