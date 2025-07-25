// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Stores.Serialization;
using Duende.IdentityServer.Validation;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// The IdentityServerOptions class is the top level container for all configuration settings of IdentityServer.
/// </summary>
public class IdentityServerOptions
{
    /// <summary>
    /// Gets or sets the unique name of this server instance, e.g. https://myissuer.com.
    /// If not set, the issuer name is inferred from the request
    /// </summary>
    /// <value>
    /// Unique name of this server instance, e.g. https://myissuer.com
    /// </value>
    public string? IssuerUri { get; set; }

    /// <summary>
    /// Set to false to preserve the original casing of the IssuerUri. Defaults to true.
    /// </summary>
    public bool LowerCaseIssuerUri { get; set; } = true;

    /// <summary>
    /// Gets or sets the value for the JWT typ header for access tokens.
    /// </summary>
    /// <value>
    /// The JWT typ value.
    /// </value>
    public string AccessTokenJwtType { get; set; } = "at+jwt";

    /// <summary>
    /// Gets or sets the value for the JWT typ header for logout tokens.
    /// </summary>
    /// <value>
    /// The JWT typ value.
    /// </value>
    public string LogoutTokenJwtType { get; set; } = "logout+jwt";

    /// <summary>
    /// Emits an aud claim with the format issuer/resources. That's needed for some older access token validation plumbing. Defaults to false.
    /// </summary>
    public bool EmitStaticAudienceClaim { get; set; }

    /// <summary>
    /// Specifies whether scopes in JWTs are emitted as array or string
    /// </summary>
    public bool EmitScopesAsSpaceDelimitedStringInJwt { get; set; }

    /// <summary>
    /// Specifies whether authorize responses contain the iss parameter (https://www.rfc-editor.org/rfc/rfc9207.html)
    /// </summary>
    public bool EmitIssuerIdentificationResponseParameter { get; set; } = true;

    /// <summary>
    /// Specifies whether the s_hash claim gets emitted in identity tokens. Defaults to false.
    /// </summary>
    public bool EmitStateHash { get; set; }

    /// <summary>
    /// Specifies whether the JWT typ and content-type for JWT secured authorization requests is checked according to IETF spec.
    /// This might break older OIDC conformant request objects.
    /// </summary>
    public bool StrictJarValidation { get; set; }

    /// <summary>
    /// Specifies if a user's tenant claim is compared to the tenant acr_values parameter value to determine if the login page is displayed. Defaults to false.
    /// </summary>
    public bool ValidateTenantOnAuthorization { get; set; }

    /// <summary>
    /// Gets or sets the endpoint configuration.
    /// </summary>
    /// <value>
    /// The endpoints configuration.
    /// </value>
    public EndpointsOptions Endpoints { get; set; } = new EndpointsOptions();

    /// <summary>
    /// Gets or sets the discovery endpoint configuration.
    /// </summary>
    /// <value>
    /// The discovery endpoint configuration.
    /// </value>
    public DiscoveryOptions Discovery { get; set; } = new DiscoveryOptions();

    /// <summary>
    /// Gets or sets the authentication options.
    /// </summary>
    /// <value>
    /// The authentication options.
    /// </value>
    public AuthenticationOptions Authentication { get; set; } = new AuthenticationOptions();

    /// <summary>
    /// Gets or sets the events options.
    /// </summary>
    /// <value>
    /// The events options.
    /// </value>
    public EventsOptions Events { get; set; } = new EventsOptions();

    /// <summary>
    /// Gets or sets the max input length restrictions.
    /// </summary>
    /// <value>
    /// The length restrictions.
    /// </value>
    public InputLengthRestrictions InputLengthRestrictions { get; set; } = new InputLengthRestrictions();

    /// <summary>
    /// Gets or sets the options for the user interaction.
    /// </summary>
    /// <value>
    /// The user interaction options.
    /// </value>
    public UserInteractionOptions UserInteraction { get; set; } = new UserInteractionOptions();

    /// <summary>
    /// Gets or sets the caching options.
    /// </summary>
    /// <value>
    /// The caching options.
    /// </value>
    public CachingOptions Caching { get; set; } = new CachingOptions();

    /// <summary>
    /// Gets or sets the cors options.
    /// </summary>
    /// <value>
    /// The cors options.
    /// </value>
    public CorsOptions Cors { get; set; } = new CorsOptions();

    /// <summary>
    /// Gets or sets the Content Security Policy options.
    /// </summary>
    public CspOptions Csp { get; set; } = new CspOptions();

    /// <summary>
    /// Gets or sets the validation options.
    /// </summary>
    public ValidationOptions Validation { get; set; } = new ValidationOptions();

    /// <summary>
    /// Gets or sets the device flow options.
    /// </summary>
    public DeviceFlowOptions DeviceFlow { get; set; } = new DeviceFlowOptions();

    /// <summary>
    /// Gets or sets the CIBA options.
    /// </summary>
    public CibaOptions Ciba { get; set; } = new CibaOptions();

    /// <summary>
    /// Gets or sets the logging options
    /// </summary>
    public LoggingOptions Logging { get; set; } = new LoggingOptions();

    /// <summary>
    /// Gets or sets the mutual TLS options.
    /// </summary>
    public MutualTlsOptions MutualTls { get; set; } = new MutualTlsOptions();

    /// <summary>
    /// Gets or sets the signing key management options.
    /// </summary>
    public KeyManagementOptions KeyManagement { get; set; } = new KeyManagementOptions();

    /// <summary>
    /// Options for persisted grants.
    /// </summary>
    public PersistentGrantOptions PersistentGrants { get; set; } = new PersistentGrantOptions();

    /// <summary>
    /// Options for DPoP.
    /// </summary>
    public DPoPOptions DPoP { get; set; } = new DPoPOptions();

    /// <summary>
    /// Gets or sets the license key.
    /// </summary>
    public string? LicenseKey { get; set; }

    /// <summary>
    /// Options for dynamic external providers.
    /// </summary>
    public DynamicProviderOptions DynamicProviders { get; set; } = new DynamicProviderOptions();

    /// <summary>
    /// Options for server-side sessions.
    /// </summary>
    public ServerSideSessionOptions ServerSideSessions { get; set; } = new ServerSideSessionOptions();

    /// <summary>
    /// Options for Pushed Authorization Requests (PAR).
    /// </summary>
    public PushedAuthorizationOptions PushedAuthorization { get; set; } = new PushedAuthorizationOptions();

    /// <summary>
    /// The allowed clock skew for JWT lifetime validation. This setting controls the clock skew of lifetime validation
    /// for all JWTs except DPoP proofs, including
    /// <list type="bullet">
    /// <item>JWT access tokens passed to the user info, introspection, and local api endpoints</item>
    /// <item>Authentication JWTs used in private_key_jwt authentication</item>
    /// <item> JWT secured authorization requests (JAR request objects)</item>
    /// <item> Custom usage of the <see cref="TokenValidator"/>, such as in a token exchange implementation.</item>
    /// </list>
    ///
    /// Defaults to five minutes.
    /// </summary>
    public TimeSpan JwtValidationClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// <para>
    /// Specifies the allowed signature algorithms for JWT secured authorization requests (JAR). The "alg" header of JAR
    /// request objects is validated against this collection, and the
    /// request_object_signing_alg_values_supported discovery property is populated with these values.
    /// </para>
    /// <para>
    /// Defaults to [RS256, RS384, RS512, PS256, PS384, PS512, ES256, ES384, ES512], which allows
    /// the RSA, Probabilistic RSA, or ECDSA signing algorithms with 256, 384, or 512-bit SHA hashing.
    /// </para>
    /// <para>
    /// If set to an empty collection, all algorithms are allowed, but the request_object_signing_alg_values_supported
    /// will not be set. Explicitly listing the expected values is recommended.
    ///</para>
    /// </summary>
    public ICollection<string> SupportedRequestObjectSigningAlgorithms { get; set; } =
    [
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,

        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,

        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512,
    ];

    /// <summary>
    /// <para>
    /// Specifies the allowed signature algorithms for client authentication using client assertions (the
    /// private_key_jwt parameter). The "alg" header of client assertions is validated against this collection, and the
    /// token_endpoint_auth_signing_alg_values_supported discovery property is populated with these values.
    /// </para>
    /// <para>
    /// Defaults to [RS256, RS384, RS512, PS256, PS384, PS512, ES256, ES384, ES512], which allows
    /// the RSA, Probabilistic RSA, or ECDSA signing algorithms with 256, 384, or 512-bit SHA hashing.
    /// </para>
    /// <para>
    /// If set to an empty collection, all algorithms are allowed, but the
    /// token_endpoint_auth_signing_alg_values_supported will not be set. Explicitly listing the expected values is
    /// recommended.
    ///</para>
    /// </summary>
    public ICollection<string> SupportedClientAssertionSigningAlgorithms { get; set; } = [
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,

        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,

        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512,
    ];

    /// <summary>
    /// Gets or sets the options for enabling and configuring preview features in the server.
    /// Preview features provide access to experimental or in-progress functionality that may undergo
    /// further changes before being finalized.
    /// </summary>
    /// <value>
    /// Options for configuring preview features in the server.
    /// </value>
    public PreviewFeatureOptions Preview { get; set; } = new PreviewFeatureOptions();

    /// <summary>
    /// Options that control the diagnostic data that is logged by IdentityServer.
    /// </summary>
    public DiagnosticOptions Diagnostics { get; set; } = new DiagnosticOptions();
}
