// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Buffers.Text;
using System.Security.Cryptography;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.ResponseHandling;

/// <summary>
/// Default implementation of the discovery endpoint response generator
/// </summary>
/// <seealso cref="IDiscoveryResponseGenerator" />
public class DiscoveryResponseGenerator : IDiscoveryResponseGenerator
{
    /// <summary>
    /// The options
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// The extension grants validator
    /// </summary>
    protected readonly ExtensionGrantValidator ExtensionGrants;

    /// <summary>
    /// The key material service
    /// </summary>
    protected readonly IKeyMaterialService Keys;

    /// <summary>
    /// The resource owner validator
    /// </summary>
    protected readonly IResourceOwnerPasswordValidator ResourceOwnerValidator;

    /// <summary>
    /// The resource store
    /// </summary>
    protected readonly IResourceStore ResourceStore;

    /// <summary>
    /// The secret parsers
    /// </summary>
    protected readonly ISecretsListParser SecretParsers;

    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    private static readonly string[] SubjectTypesSupported = ["public"];

    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryResponseGenerator"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="keys">The keys.</param>
    /// <param name="extensionGrants">The extension grants.</param>
    /// <param name="secretParsers">The secret parsers.</param>
    /// <param name="resourceOwnerValidator">The resource owner validator.</param>
    /// <param name="logger">The logger.</param>
    public DiscoveryResponseGenerator(
        IdentityServerOptions options,
        IResourceStore resourceStore,
        IKeyMaterialService keys,
        ExtensionGrantValidator extensionGrants,
        ISecretsListParser secretParsers,
        IResourceOwnerPasswordValidator resourceOwnerValidator,
        ILogger<DiscoveryResponseGenerator> logger)
    {
        Options = options;
        ResourceStore = resourceStore;
        Keys = keys;
        ExtensionGrants = extensionGrants;
        SecretParsers = secretParsers;
        ResourceOwnerValidator = resourceOwnerValidator;
        Logger = logger;
    }

    /// <summary>
    /// Creates the discovery document.
    /// </summary>
    /// <param name="baseUrl">The base URL.</param>
    /// <param name="issuerUri">The issuer URI.</param>
    public virtual async Task<Dictionary<string, object>> CreateDiscoveryDocumentAsync(string baseUrl, string issuerUri)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("DiscoveryResponseGenerator.CreateDiscoveryDocument");

        baseUrl = baseUrl.EnsureTrailingSlash();

        var entries = new Dictionary<string, object>
        {
            { OidcConstants.Discovery.Issuer, issuerUri }
        };

        // jwks
        if (Options.Discovery.ShowKeySet)
        {
            if ((await Keys.GetValidationKeysAsync()).Any())
            {
                entries.Add(OidcConstants.Discovery.JwksUri, baseUrl + ProtocolRoutePaths.DiscoveryWebKeys);
            }
        }

        // endpoints
        if (Options.Discovery.ShowEndpoints)
        {
            if (Options.Endpoints.EnableAuthorizeEndpoint)
            {
                entries.Add(OidcConstants.Discovery.AuthorizationEndpoint, baseUrl + ProtocolRoutePaths.Authorize);
            }

            if (Options.Endpoints.EnableTokenEndpoint)
            {
                entries.Add(OidcConstants.Discovery.TokenEndpoint, baseUrl + ProtocolRoutePaths.Token);
            }

            if (Options.Endpoints.EnableUserInfoEndpoint)
            {
                entries.Add(OidcConstants.Discovery.UserInfoEndpoint, baseUrl + ProtocolRoutePaths.UserInfo);
            }

            if (Options.Endpoints.EnableEndSessionEndpoint)
            {
                entries.Add(OidcConstants.Discovery.EndSessionEndpoint, baseUrl + ProtocolRoutePaths.EndSession);
            }

            if (Options.Endpoints.EnableCheckSessionEndpoint)
            {
                entries.Add(OidcConstants.Discovery.CheckSessionIframe, baseUrl + ProtocolRoutePaths.CheckSession);
            }

            if (Options.Endpoints.EnableTokenRevocationEndpoint)
            {
                entries.Add(OidcConstants.Discovery.RevocationEndpoint, baseUrl + ProtocolRoutePaths.Revocation);
            }

            if (Options.Endpoints.EnableIntrospectionEndpoint)
            {
                entries.Add(OidcConstants.Discovery.IntrospectionEndpoint, baseUrl + ProtocolRoutePaths.Introspection);
            }

            if (Options.Endpoints.EnableDeviceAuthorizationEndpoint)
            {
                entries.Add(OidcConstants.Discovery.DeviceAuthorizationEndpoint, baseUrl + ProtocolRoutePaths.DeviceAuthorization);
            }

            if (Options.Endpoints.EnableBackchannelAuthenticationEndpoint)
            {
                entries.Add(OidcConstants.Discovery.BackchannelAuthenticationEndpoint, baseUrl + ProtocolRoutePaths.BackchannelAuthentication);
            }

            if (Options.Endpoints.EnablePushedAuthorizationEndpoint)
            {
                entries.Add(OidcConstants.Discovery.PushedAuthorizationRequestEndpoint, baseUrl + ProtocolRoutePaths.PushedAuthorization);
                entries.Add(OidcConstants.Discovery.RequirePushedAuthorizationRequests, Options.PushedAuthorization.Required);
            }

            if (Options.MutualTls.Enabled)
            {
                var mtlsEndpoints = new Dictionary<string, string>();

                if (Options.Endpoints.EnableTokenEndpoint)
                {
                    mtlsEndpoints.Add(OidcConstants.Discovery.TokenEndpoint, ConstructMtlsEndpoint(ProtocolRoutePaths.Token));
                }
                if (Options.Endpoints.EnableTokenRevocationEndpoint)
                {
                    mtlsEndpoints.Add(OidcConstants.Discovery.RevocationEndpoint, ConstructMtlsEndpoint(ProtocolRoutePaths.Revocation));
                }
                if (Options.Endpoints.EnableIntrospectionEndpoint)
                {
                    mtlsEndpoints.Add(OidcConstants.Discovery.IntrospectionEndpoint, ConstructMtlsEndpoint(ProtocolRoutePaths.Introspection));
                }
                if (Options.Endpoints.EnableDeviceAuthorizationEndpoint)
                {
                    mtlsEndpoints.Add(OidcConstants.Discovery.DeviceAuthorizationEndpoint, ConstructMtlsEndpoint(ProtocolRoutePaths.DeviceAuthorization));
                }

                if (Options.Endpoints.EnablePushedAuthorizationEndpoint)
                {
                    mtlsEndpoints.Add(OidcConstants.Discovery.PushedAuthorizationRequestEndpoint, ConstructMtlsEndpoint(ProtocolRoutePaths.PushedAuthorization));
                }

                if (mtlsEndpoints.Count > 0)
                {
                    entries.Add(OidcConstants.Discovery.MtlsEndpointAliases, mtlsEndpoints);
                }

                //Note: This logic is currently duplicated in the DefaultMtlsEndpointGenerator as adding a new
                //dependency here would be a breaking change in a non-major release.
                string ConstructMtlsEndpoint(string endpoint)
                {
                    // path based
                    if (Options.MutualTls.DomainName.IsMissing())
                    {
                        return baseUrl + endpoint.Replace(ProtocolRoutePaths.ConnectPathPrefix, ProtocolRoutePaths.MtlsPathPrefix, StringComparison.InvariantCulture);
                    }

                    // domain based
                    if (Options.MutualTls.DomainName.Contains('.', StringComparison.InvariantCulture))
                    {
                        return $"https://{Options.MutualTls.DomainName}/{endpoint}";
                    }
                    // sub-domain based
                    else
                    {
                        var parts = baseUrl.Split("://");
                        return $"https://{Options.MutualTls.DomainName}.{parts[1]}{endpoint}";
                    }
                }
            }
        }

        // logout
        if (Options.Endpoints.EnableEndSessionEndpoint)
        {
            entries.Add(OidcConstants.Discovery.FrontChannelLogoutSupported, true);
            entries.Add(OidcConstants.Discovery.FrontChannelLogoutSessionSupported, true);
            entries.Add(OidcConstants.Discovery.BackChannelLogoutSupported, true);
            entries.Add(OidcConstants.Discovery.BackChannelLogoutSessionSupported, true);
        }

        // scopes and claims
        if (Options.Discovery.ShowIdentityScopes ||
            Options.Discovery.ShowApiScopes ||
            Options.Discovery.ShowClaims)
        {
            var resources = await ResourceStore.GetAllEnabledResourcesAsync();
            var scopes = new List<string>();

            // scopes
            if (Options.Discovery.ShowIdentityScopes)
            {
                scopes.AddRange(resources.IdentityResources.Where(x => x.ShowInDiscoveryDocument).Select(x => x.Name));
            }

            if (Options.Discovery.ShowApiScopes)
            {
                var apiScopes = from scope in resources.ApiScopes
                                where scope.ShowInDiscoveryDocument
                                select scope.Name;

                scopes.AddRange(apiScopes);
                scopes.Add(StandardScopes.OfflineAccess);
            }

            if (scopes.Count > 0)
            {
                entries.Add(OidcConstants.Discovery.ScopesSupported, scopes.ToArray());
            }

            // claims
            if (Options.Discovery.ShowClaims)
            {
                var claims = new List<string>();

                // add non-hidden identity scopes related claims
                claims.AddRange(resources.IdentityResources.Where(x => x.ShowInDiscoveryDocument).SelectMany(x => x.UserClaims));
                claims.AddRange(resources.ApiResources.Where(x => x.ShowInDiscoveryDocument).SelectMany(x => x.UserClaims));
                claims.AddRange(resources.ApiScopes.Where(x => x.ShowInDiscoveryDocument).SelectMany(x => x.UserClaims));

                entries.Add(OidcConstants.Discovery.ClaimsSupported, claims.Distinct().ToArray());
            }
        }

        // grant types
        if (Options.Discovery.ShowGrantTypes)
        {
            var standardGrantTypes = new List<string>
            {
                OidcConstants.GrantTypes.AuthorizationCode,
                OidcConstants.GrantTypes.ClientCredentials,
                OidcConstants.GrantTypes.RefreshToken,
                OidcConstants.GrantTypes.Implicit
            };

            if (!(ResourceOwnerValidator is NotSupportedResourceOwnerPasswordValidator))
            {
                standardGrantTypes.Add(OidcConstants.GrantTypes.Password);
            }

            if (Options.Endpoints.EnableDeviceAuthorizationEndpoint)
            {
                standardGrantTypes.Add(OidcConstants.GrantTypes.DeviceCode);
            }

            if (Options.Endpoints.EnableBackchannelAuthenticationEndpoint)
            {
                standardGrantTypes.Add(OidcConstants.GrantTypes.Ciba);
            }

            var showGrantTypes = new List<string>(standardGrantTypes);

            if (Options.Discovery.ShowExtensionGrantTypes)
            {
                showGrantTypes.AddRange(ExtensionGrants.GetAvailableGrantTypes());
            }

            entries.Add(OidcConstants.Discovery.GrantTypesSupported, showGrantTypes.ToArray());
        }

        // response types
        if (Options.Discovery.ShowResponseTypes)
        {
            entries.Add(OidcConstants.Discovery.ResponseTypesSupported, Constants.SupportedResponseTypes.ToArray());
        }

        // response modes
        if (Options.Discovery.ShowResponseModes)
        {
            entries.Add(OidcConstants.Discovery.ResponseModesSupported, Constants.SupportedResponseModes.ToArray());
        }

        var supportedAuthMethods = GetSupportedAuthMethods();
        // misc
        if (Options.Discovery.ShowTokenEndpointAuthenticationMethods)
        {
            entries.Add(OidcConstants.Discovery.TokenEndpointAuthenticationMethodsSupported, supportedAuthMethods);
            AddSigningAlgorithmsForEndpointIfNeeded(OidcConstants.Discovery.TokenEndpointAuthSigningAlgorithmsSupported, entries, supportedAuthMethods);
        }

        if (Options.Discovery.ShowRevocationEndpointAuthenticationMethods)
        {
            entries.Add(OidcConstants.Discovery.RevocationEndpointAuthenticationMethodsSupported, supportedAuthMethods);
            AddSigningAlgorithmsForEndpointIfNeeded(OidcConstants.Discovery.RevocationEndpointAuthSigningAlgorithmsSupported, entries, supportedAuthMethods);
        }

        if (Options.Discovery.ShowIntrospectionEndpointAuthenticationMethods)
        {
            entries.Add(OidcConstants.Discovery.IntrospectionEndpointAuthenticationMethodsSupported, supportedAuthMethods);
            AddSigningAlgorithmsForEndpointIfNeeded(OidcConstants.Discovery.IntrospectionEndpointAuthSigningAlgorithmsSupported, entries, supportedAuthMethods);
        }

        var signingCredentials = await Keys.GetAllSigningCredentialsAsync();
        if (signingCredentials.Any())
        {
            var signingAlgorithms = signingCredentials.Select(c => c.Algorithm).Distinct();
            entries.Add(OidcConstants.Discovery.IdTokenSigningAlgorithmsSupported, signingAlgorithms);

            if (Options.Endpoints.EnableUserInfoEndpoint)
            {
                entries.Add(OidcConstants.Discovery.UserInfoSigningAlgorithmsSupported, signingAlgorithms);
            }

            if (Options.Endpoints.EnableIntrospectionEndpoint)
            {
                entries.Add(OidcConstants.Discovery.IntrospectionSigningAlgorithmsSupported, signingAlgorithms);
            }
        }

        entries.Add(OidcConstants.Discovery.SubjectTypesSupported, SubjectTypesSupported);
        entries.Add(OidcConstants.Discovery.CodeChallengeMethodsSupported, new[] { OidcConstants.CodeChallengeMethods.Plain, OidcConstants.CodeChallengeMethods.Sha256 });

        if (Options.Endpoints.EnableAuthorizeEndpoint)
        {
            entries.Add(OidcConstants.Discovery.RequestParameterSupported, true);

            if (!IEnumerableExtensions.IsNullOrEmpty(Options.SupportedRequestObjectSigningAlgorithms))
            {
                entries.Add(OidcConstants.Discovery.RequestObjectSigningAlgorithmsSupported,
                    Options.SupportedRequestObjectSigningAlgorithms);
            }

            if (Options.Endpoints.EnableJwtRequestUri)
            {
                entries.Add(OidcConstants.Discovery.RequestUriParameterSupported, true);
            }

            if (Options.UserInteraction.PromptValuesSupported?.Count > 0)
            {
                entries.Add(OidcConstants.Discovery.PromptValuesSupported, Options.UserInteraction.PromptValuesSupported.ToArray());
            }
        }

        entries.Add(OidcConstants.Discovery.AuthorizationResponseIssParameterSupported, Options.EmitIssuerIdentificationResponseParameter);

        if (Options.MutualTls.Enabled)
        {
            entries.Add(OidcConstants.Discovery.TlsClientCertificateBoundAccessTokens, true);
        }

        if (Options.Endpoints.EnableBackchannelAuthenticationEndpoint)
        {
            entries.Add(OidcConstants.Discovery.BackchannelTokenDeliveryModesSupported,
                new[] { OidcConstants.BackchannelTokenDeliveryModes.Poll });
            entries.Add(OidcConstants.Discovery.BackchannelUserCodeParameterSupported, true);

            if (!IEnumerableExtensions.IsNullOrEmpty(Options.SupportedRequestObjectSigningAlgorithms))
            {
                entries.Add(OidcConstants.Discovery.BackchannelAuthenticationRequestSigningAlgValuesSupported,
                    Options.SupportedRequestObjectSigningAlgorithms);
            }
        }

        if (Options.Endpoints.EnableTokenEndpoint &&
            !IEnumerableExtensions.IsNullOrEmpty(Options.DPoP.SupportedDPoPSigningAlgorithms))
        {
            entries.Add(OidcConstants.Discovery.DPoPSigningAlgorithmsSupported, Options.DPoP.SupportedDPoPSigningAlgorithms);
        }

        switch (Options.Discovery.DynamicClientRegistration.RegistrationEndpointMode)
        {
            case RegistrationEndpointMode.Static:
                if (Options.Discovery.DynamicClientRegistration.StaticRegistrationEndpoint != null)
                {
                    entries.Add(OidcConstants.Discovery.RegistrationEndpoint, Options.Discovery.DynamicClientRegistration.StaticRegistrationEndpoint.ToString());
                }
                break;
            case RegistrationEndpointMode.Inferred:
                entries.Add(OidcConstants.Discovery.RegistrationEndpoint, baseUrl + ProtocolRoutePaths.DynamicClientRegistration);
                break;
            case RegistrationEndpointMode.None:
            default:
                break;
        }

        // custom entries
        if (!IEnumerableExtensions.IsNullOrEmpty(Options.Discovery.CustomEntries))
        {
            foreach (var (key, value) in Options.Discovery.CustomEntries)
            {
#pragma warning disable CA1864 // Keep to avoid unnecessary string manipulations if we have duplicate keys
                if (entries.ContainsKey(key))
#pragma warning restore CA1864
                {
                    Logger.LogError("Discovery custom entry {key} cannot be added, because it already exists.", key);
                }
                else
                {
                    if (value is string customValueString)
                    {
                        if (customValueString.StartsWith("~/", StringComparison.Ordinal) && Options.Discovery.ExpandRelativePathsInCustomEntries)
                        {
                            entries.Add(key, string.Concat(baseUrl, customValueString.AsSpan(2)));
                            continue;
                        }
                    }

                    entries.Add(key, value);
                }
            }
        }

        return entries;
    }

    /// <summary>
    /// Creates the JWK document.
    /// </summary>
    public virtual async Task<IEnumerable<Models.JsonWebKey>> CreateJwkDocumentAsync()
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("DiscoveryResponseGenerator.CreateJwkDocument");

        var webKeys = new List<Models.JsonWebKey>();

        foreach (var key in await Keys.GetValidationKeysAsync())
        {
            if (key.Key is X509SecurityKey x509Key)
            {
                var cert64 = Convert.ToBase64String(x509Key.Certificate.RawData);
                var thumbprint = Base64Url.EncodeToString(x509Key.Certificate.GetCertHash());

                if (x509Key.PublicKey is RSA rsa)
                {
                    var parameters = rsa.ExportParameters(false);
                    var exponent = Base64Url.EncodeToString(parameters.Exponent);
                    var modulus = Base64Url.EncodeToString(parameters.Modulus);

                    var rsaJsonWebKey = new Models.JsonWebKey
                    {
                        kty = "RSA",
                        use = "sig",
                        kid = x509Key.KeyId,
                        x5t = thumbprint,
                        e = exponent,
                        n = modulus,
                        x5c = new[] { cert64 },
                        alg = key.SigningAlgorithm
                    };
                    webKeys.Add(rsaJsonWebKey);
                }
                else if (x509Key.PublicKey is ECDsa ecdsa)
                {
                    var parameters = ecdsa.ExportParameters(false);
                    var x = Base64Url.EncodeToString(parameters.Q.X);
                    var y = Base64Url.EncodeToString(parameters.Q.Y);

                    var ecdsaJsonWebKey = new Models.JsonWebKey
                    {
                        kty = "EC",
                        use = "sig",
                        kid = x509Key.KeyId,
                        x5t = thumbprint,
                        x = x,
                        y = y,
                        crv = CryptoHelper.GetCrvValueFromCurve(parameters.Curve),
                        x5c = new[] { cert64 },
                        alg = key.SigningAlgorithm
                    };
                    webKeys.Add(ecdsaJsonWebKey);
                }
                else
                {
                    throw new InvalidOperationException($"key type: {x509Key.PublicKey.GetType().Name} not supported.");
                }
            }
            else if (key.Key is RsaSecurityKey rsaKey)
            {
                var parameters = rsaKey.Rsa?.ExportParameters(false) ?? rsaKey.Parameters;
                var exponent = Base64Url.EncodeToString(parameters.Exponent);
                var modulus = Base64Url.EncodeToString(parameters.Modulus);

                var webKey = new Models.JsonWebKey
                {
                    kty = "RSA",
                    use = "sig",
                    kid = rsaKey.KeyId,
                    e = exponent,
                    n = modulus,
                    alg = key.SigningAlgorithm
                };

                webKeys.Add(webKey);
            }
            else if (key.Key is ECDsaSecurityKey ecdsaKey)
            {
                var parameters = ecdsaKey.ECDsa.ExportParameters(false);
                var x = Base64Url.EncodeToString(parameters.Q.X);
                var y = Base64Url.EncodeToString(parameters.Q.Y);

                var ecdsaJsonWebKey = new Models.JsonWebKey
                {
                    kty = "EC",
                    use = "sig",
                    kid = ecdsaKey.KeyId,
                    x = x,
                    y = y,
                    crv = CryptoHelper.GetCrvValueFromCurve(parameters.Curve),
                    alg = key.SigningAlgorithm
                };
                webKeys.Add(ecdsaJsonWebKey);
            }
            else if (key.Key is JsonWebKey jsonWebKey)
            {
                var webKey = new Models.JsonWebKey
                {
                    kty = jsonWebKey.Kty,
                    use = jsonWebKey.Use ?? "sig",
                    kid = jsonWebKey.Kid,
                    x5t = jsonWebKey.X5t,
                    e = jsonWebKey.E,
                    n = jsonWebKey.N,
                    x5c = jsonWebKey.X5c?.Count == 0 ? null : jsonWebKey.X5c.ToArray(),
                    alg = jsonWebKey.Alg,
                    crv = jsonWebKey.Crv,
                    x = jsonWebKey.X,
                    y = jsonWebKey.Y
                };

                webKeys.Add(webKey);
            }
        }

        return webKeys;
    }

    private List<string> GetSupportedAuthMethods()
    {
        var types = SecretParsers.GetAvailableAuthenticationMethods().ToList();
        if (Options.MutualTls.Enabled)
        {
            types.Add(OidcConstants.EndpointAuthenticationMethods.TlsClientAuth);
            types.Add(OidcConstants.EndpointAuthenticationMethods.SelfSignedTlsClientAuth);
        }

        return types;
    }

    private void AddSigningAlgorithmsForEndpointIfNeeded(string key, Dictionary<string, object> entries, IEnumerable<string> supportedAuthMethods)
    {
        if (supportedAuthMethods.Contains(OidcConstants.EndpointAuthenticationMethods.PrivateKeyJwt) && !IEnumerableExtensions.IsNullOrEmpty(Options.SupportedClientAssertionSigningAlgorithms))
        {
            entries.Add(key, Options.SupportedClientAssertionSigningAlgorithms);
        }
    }
}
