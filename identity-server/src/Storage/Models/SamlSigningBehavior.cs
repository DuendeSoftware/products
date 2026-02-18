// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Specifies the signing behavior for SAML messages and assertions.
/// </summary>
public enum SamlSigningBehavior
{
    /// <summary>
    /// Do not sign the SAML Response or Assertion.
    /// Only use for testing or non-production scenarios.
    /// </summary>
    DoNotSign = 0,

    /// <summary>
    /// Sign only the Response element.
    /// The signature wraps the entire response including the assertion.
    /// </summary>
    SignResponse = 1,

    /// <summary>
    /// Sign only the Assertion element (within the Response).
    /// This is the most common and recommended strategy.
    /// Works with all SAML 2.0 compliant Service Providers.
    /// </summary>
    SignAssertion = 2,

    /// <summary>
    /// Sign both the Response and the Assertion.
    /// Provides maximum security but increases message size.
    /// Use for high-security environments.
    /// </summary>
    SignBoth = 3
}
