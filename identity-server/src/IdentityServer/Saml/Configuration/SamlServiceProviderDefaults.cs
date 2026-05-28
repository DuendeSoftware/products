// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Saml.Configuration;

/// <summary>
/// Default constants for the standalone SAML 2.0 Service Provider handler.
/// </summary>
public static class SamlServiceProviderDefaults
{
    /// <summary>
    /// Default authentication scheme name for the SAML 2.0 Service Provider.
    /// </summary>
    public const string Scheme = "Saml2";

    /// <summary>
    /// Default display name for the SAML 2.0 Service Provider authentication scheme.
    /// </summary>
    public const string DisplayName = "SAML 2.0";

    /// <summary>
    /// Default module path for the Saml2 handler endpoints.
    /// </summary>
    public const string ModulePath = "/Saml2";
}
