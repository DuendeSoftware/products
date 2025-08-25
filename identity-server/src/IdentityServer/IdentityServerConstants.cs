// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer;

public static class IdentityServerConstants
{
    public const string LocalIdentityProvider = "local";
    public const string DefaultCookieAuthenticationScheme = "idsrv";
    public const string SignoutScheme = "idsrv";
    public const string ExternalCookieAuthenticationScheme = "idsrv.external";
    public const string DefaultCheckSessionCookieName = "idsrv.session";
    public const string AccessTokenAudience = "{0}resources";

    public const string JwtRequestClientKey = "idsrv.jwtrequesturi.client";

    public const string PushedAuthorizationRequestUri = "urn:ietf:params:oauth:request_uri";

    public const string ForceCookieRenewalFlag = "idsrv.renew";

    /// <summary>
    /// Constants for local IdentityServer access token authentication.
    /// </summary>
    public static class LocalApi
    {
        /// <summary>
        /// The authentication scheme when using the AddLocalApi helper.
        /// </summary>
        public const string AuthenticationScheme = "IdentityServerAccessToken";

        /// <summary>
        /// The API scope name when using the AddLocalApiAuthentication helper.
        /// </summary>
        public const string ScopeName = "IdentityServerApi";

        /// <summary>
        /// The authorization policy name when using the AddLocalApiAuthentication helper.
        /// </summary>
        public const string PolicyName = AuthenticationScheme;
    }

    public static class ProtocolTypes
    {
        public const string OpenIdConnect = "oidc";
        public const string WsFederation = "wsfed";
        public const string Saml2p = "saml2p";
    }

    public static class TokenTypes
    {
        public const string IdentityToken = "id_token";
        public const string AccessToken = "access_token";
        public const string LogoutToken = "logout_token";
    }

    public static class ClaimValueTypes
    {
        public const string Json = "json";
    }

    public static class ParsedSecretTypes
    {
        public const string NoSecret = "NoSecret";
        public const string SharedSecret = "SharedSecret";
        public const string X509Certificate = "X509Certificate";
        public const string JwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
    }

    public static class SecretTypes
    {
        public const string SharedSecret = "SharedSecret";
        public const string X509CertificateThumbprint = "X509Thumbprint";
        public const string X509CertificateName = "X509Name";
        public const string X509CertificateBase64 = "X509CertificateBase64";
        public const string JsonWebKey = "JWK";
    }

    public static class ProfileDataCallers
    {
        public const string UserInfoEndpoint = "UserInfoEndpoint";
        public const string ClaimsProviderIdentityToken = "ClaimsProviderIdentityToken";
        public const string ClaimsProviderAccessToken = "ClaimsProviderAccessToken";
    }

    public static class ProfileIsActiveCallers
    {
        public const string AuthorizeEndpoint = "AuthorizeEndpoint";
        public const string IdentityTokenValidation = "IdentityTokenValidation";
        public const string AccessTokenValidation = "AccessTokenValidation";
        public const string ResourceOwnerValidation = "ResourceOwnerValidation";
        public const string ExtensionGrantValidation = "ExtensionGrantValidation";
        public const string RefreshTokenValidation = "RefreshTokenValidation";
        public const string AuthorizationCodeValidation = "AuthorizationCodeValidation";
        public const string UserInfoRequestValidation = "UserInfoRequestValidation";
        public const string DeviceCodeValidation = "DeviceCodeValidation";
        public const string BackchannelAuthenticationRequestIdValidation = "BackchannelAuthenticationRequestIdValidation";
    }


    public readonly static IEnumerable<string> SupportedSigningAlgorithms = new List<string>
    {
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,

        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,

        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512
    };

    public enum RsaSigningAlgorithm
    {
        RS256,
        RS384,
        RS512,

        PS256,
        PS384,
        PS512
    }

    public enum ECDsaSigningAlgorithm
    {
        ES256,
        ES384,
        ES512
    }

    public static class StandardScopes
    {
        /// <summary>REQUIRED. Informs the Authorization Server that the Client is making an OpenID Connect request. If the <c>openid</c> scope value is not present, the behavior is entirely unspecified.</summary>
        public const string OpenId = "openid";

        /// <summary>OPTIONAL. This scope value requests access to the End-User's default profile Claims, which are: <c>name</c>, <c>family_name</c>, <c>given_name</c>, <c>middle_name</c>, <c>nickname</c>, <c>preferred_username</c>, <c>profile</c>, <c>picture</c>, <c>website</c>, <c>gender</c>, <c>birthdate</c>, <c>zoneinfo</c>, <c>locale</c>, and <c>updated_at</c>.</summary>
        public const string Profile = "profile";

        /// <summary>OPTIONAL. This scope value requests access to the <c>email</c> and <c>email_verified</c> Claims.</summary>
        public const string Email = "email";

        /// <summary>OPTIONAL. This scope value requests access to the <c>address</c> Claim.</summary>
        public const string Address = "address";

        /// <summary>OPTIONAL. This scope value requests access to the <c>phone_number</c> and <c>phone_number_verified</c> Claims.</summary>
        public const string Phone = "phone";

        /// <summary>This scope value MUST NOT be used with the OpenID Connect Implicit Client Implementer's Guide 1.0. See the OpenID Connect Basic Client Implementer's Guide 1.0 (http://openid.net/specs/openid-connect-implicit-1_0.html#OpenID.Basic) for its usage in that subset of OpenID Connect.</summary>
        public const string OfflineAccess = "offline_access";
    }

    public static class PersistedGrantTypes
    {
        public const string AuthorizationCode = "authorization_code";
        public const string BackChannelAuthenticationRequest = "ciba";
        public const string ReferenceToken = "reference_token";
        public const string RefreshToken = "refresh_token";
        public const string UserConsent = "user_consent";
        public const string DeviceCode = "device_code";
        public const string UserCode = "user_code";
    }

    public static class UserCodeTypes
    {
        public const string Numeric = "Numeric";
    }

    public static class HttpClients
    {
        public const int DefaultTimeoutSeconds = 10;
        public const string JwtRequestUriHttpClient = "IdentityServer:JwtRequestUriClient";
        public const string BackChannelLogoutHttpClient = "IdentityServer:BackChannelLogoutClient";
    }

    public static class ClaimTypes
    {
        public const string Tenant = "tenant";
        public const string BackChannelLogoutReason = "logout_reason";
    }
    public static class BackChannelLogoutReasons
    {
        public const string UserLogout = "user_logout";
        public const string SessionExpiration = "session_expiration";
        public const string Terminated = "terminated";
    }

    public static class Tracing
    {
        public static readonly string Basic = Duende.IdentityServer.Tracing.TraceNames.Basic;
        public static readonly string Stores = Duende.IdentityServer.Tracing.TraceNames.Store;
        public static readonly string Cache = Duende.IdentityServer.Tracing.TraceNames.Cache;
        public static readonly string Validation = Duende.IdentityServer.Tracing.TraceNames.Validation;
        public static readonly string Services = Duende.IdentityServer.Tracing.TraceNames.Services;

        public static readonly string ServiceVersion = Duende.IdentityServer.Tracing.ServiceVersion;
    }

    public static class EndpointNames
    {
        public const string Authorize = "Authorize";
        public const string BackchannelAuthentication = "BackchannelAuthentication";
        public const string Token = "Token";
        public const string DeviceAuthorization = "DeviceAuthorization";
        public const string Discovery = "Discovery";
        public const string Jwks = "Jwks";
        public const string Introspection = "Introspection";
        public const string Revocation = "Revocation";
        public const string EndSession = "Endsession";
        public const string CheckSession = "Checksession";
        public const string UserInfo = "Userinfo";
        public const string PushedAuthorization = "PushedAuthorization";
        public const string OAuth2AuthorizationServerMetadata = "OAuth2AuthorizationServerMetadata";

    }

    public static class ContentSecurityPolicyHashes
    {
        /// <summary>
        /// The hash of the inline style used on the end session endpoint.
        /// </summary>
        public const string EndSessionStyle = "sha256-e6FQZewefmod2S/5T11pTXjzE2vn3/8GRwWOs917YE4=";

        /// <summary>
        /// The hash of the inline script used on the authorize endpoint.
        /// </summary>
        public const string AuthorizeScript = "sha256-orD0/VhH8hLqrLxKHD/HUEMdwqX6/0ve7c5hspX5VJ8=";

        /// <summary>
        /// The hash of the inline script used on the check session endpoint.
        /// </summary>
        public const string CheckSessionScript = "sha256-fa5rxHhZ799izGRP38+h4ud5QXNT0SFaFlh4eqDumBI=";
    }

    public static class ProtocolRoutePaths
    {
        public const string ConnectPathPrefix = "connect";

        public const string Authorize = ConnectPathPrefix + "/authorize";
        public const string AuthorizeCallback = Authorize + "/callback";
        public const string DiscoveryConfiguration = ".well-known/openid-configuration";
        public const string DiscoveryWebKeys = DiscoveryConfiguration + "/jwks";
        public const string BackchannelAuthentication = ConnectPathPrefix + "/ciba";
        public const string Token = ConnectPathPrefix + "/token";
        public const string Revocation = ConnectPathPrefix + "/revocation";
        public const string UserInfo = ConnectPathPrefix + "/userinfo";
        public const string Introspection = ConnectPathPrefix + "/introspect";
        public const string EndSession = ConnectPathPrefix + "/endsession";
        public const string EndSessionCallback = EndSession + "/callback";
        public const string CheckSession = ConnectPathPrefix + "/checksession";
        public const string DeviceAuthorization = ConnectPathPrefix + "/deviceauthorization";
        public const string PushedAuthorization = ConnectPathPrefix + "/par";
        public const string OAuth2AuthorizationServerMetadata = ".well-known/oauth-authorization-server/{*subPath}";


        public const string MtlsPathPrefix = ConnectPathPrefix + "/mtls";
        public const string MtlsToken = MtlsPathPrefix + "/token";
        public const string MtlsRevocation = MtlsPathPrefix + "/revocation";
        public const string MtlsIntrospection = MtlsPathPrefix + "/introspect";
        public const string MtlsDeviceAuthorization = MtlsPathPrefix + "/deviceauthorization";

        public static readonly string[] CorsPaths =
        {
            DiscoveryConfiguration,
            DiscoveryWebKeys,
            Token,
            UserInfo,
            Revocation
        };
    }
}
