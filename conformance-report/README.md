# Duende Conformance Report

_Standalone conformance assessment for OAuth 2.1 and FAPI 2.0 Security Profile compliance._

## Overview

Duende Conformance Report evaluates your IdentityServer configuration against OAuth 2.1 and FAPI 2.0 Security Profile requirements and generates an HTML report showing server and client configuration conformance.

For installation and setup instructions, see the [Duende.IdentityServer.ConformanceReport](https://www.nuget.org/packages/Duende.IdentityServer.ConformanceReport) package.

## Conformance Profiles

### OAuth 2.1

OAuth 2.1 consolidates best practices from OAuth 2.0, including mandatory PKCE, removal of deprecated grant types, and enhanced security requirements.

**Specification**: https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-16

#### Server Rules

| Rule | Name | Requirement |
|------|------|-------------|
| S01 | PKCE Support | The authorization server must support `code_challenge`/`code_verifier` (PKCE) for all clients |
| S02 | Removed Grant Types | No client may use a grant type removed by OAuth 2.1: password, or implicit or hybrid when access tokens are delivered via the browser (`AllowAccessTokensViaBrowser`) |
| S04 | Sender-Constrained Token Support | The server should support DPoP or mTLS sender-constrained tokens |
| S08 | HTTP 303 Redirects | The authorization server must not use HTTP 307 and should use 303 for redirects (§7.5.4). IdentityServer always uses 303. |
| S09 | Issuer Identification | The `iss` parameter must be emitted in the authorization response |

#### Client Rules

| Rule | Name | Requirement |
|------|------|-------------|
| C01 | OAuth 2.1 Grant Types | The password grant is prohibited. The implicit and hybrid grants are prohibited only when access tokens are delivered via the browser (`AllowAccessTokensViaBrowser`) |
| C02 | PKCE Required | PKCE must be required for code-flow clients. OAuth 2.1 (§4.1.2.1) lets confidential clients use another mitigation for authorization code injection, such as the OpenID Connect nonce, but that can't be verified from configuration, so C02 fails whenever PKCE isn't required. |
| C03 | No Plain Text PKCE | Plain text PKCE must be disabled |
| C04 | Explicit Redirect URIs | Redirect URIs must be absolute, have no fragment, use https (loopback http is accepted only if the server opts in to loopback redirection, as in FC13), and avoid risky private-use schemes. Confidential clients may have no registered redirect URIs when `AllowUnregisteredPushedRedirectUris` and the PAR endpoint are enabled. |
| C05 | Client Authentication | The client_credentials grant requires a confidential client; public authorization_code clients must use PKCE |
| C07 | Sender-Constrained Tokens | DPoP or mTLS recommended |
| C08 | Authorization Code Lifetime | Authorization code lifetime should not exceed 10 minutes (600 seconds) |
| C09 | Refresh Token Rotation | Public clients must detect refresh token replay (§4.3.1). DPoP-bound refresh tokens pass. One-time-only refresh tokens warn, because IdentityServer doesn't revoke the grant on replay without customization. Neither fails. |
| C11 | Secure Client Authentication | Confidential clients should use private_key_jwt or mTLS instead of a shared secret |

### FAPI 2.0 Security Profile

FAPI 2.0 Security Profile defines security requirements for high-risk scenarios such as financial services, requiring stronger authentication, authorization, and token security.

**Specification**: https://openid.net/specs/fapi-security-profile-2_0-final.html

#### Server Rules

| Rule | Name | Requirement |
|------|------|-------------|
| FS01 | PAR Required | PAR must be enabled and required |
| FS03 | Client Assertion Signing Algorithms | `SupportedClientAssertionSigningAlgorithms` (private_key_jwt) must allow only PS256 or ES256 |
| FS04 | PAR Lifetime | PAR lifetime <600 seconds |
| FS05 | Sender-Constraining Mechanisms | mTLS must be enabled or DPoP proof algorithms configured (§5.3.2.1) |
| FS06 | Issuer Identification | Issuer identification response parameter required |
| FS07 | HTTP 303 Redirects | The authorization server must not use HTTP 307 and should use 303 for redirects (§5.3.2.2). IdentityServer always uses 303. |
| FS08 | PKCE Support | The authorization server must require PKCE with S256 (§5.3.2.2). IdentityServer always supports S256; per-client enforcement is FC03. |
| FS09 | Token Signing Algorithms | `KeyManagement.SigningAlgorithms` must use only PS256 or ES256 |
| FS10 | DPoP Proof Algorithms | `DPoP.SupportedDPoPSigningAlgorithms` must allow only PS256 or ES256; not applicable when DPoP is disabled (empty) and mTLS is enabled |
| FS11 | Discovery Endpoint | Discovery endpoint must be enabled |
| FS12 | Issuer URI Scheme | `IssuerUri`, if set, must use https |
| FS13 | Request Object Signing Algorithms | `SupportedRequestObjectSigningAlgorithms` (JAR) must allow only PS256 or ES256 |

#### Client Rules

| Rule | Name | Requirement |
|------|------|-------------|
| FC01 | Grant Types | Implicit, hybrid, and password grants are prohibited; other grants (e.g. device_code, CIBA) are allowed |
| FC02 | Confidential Client | All clients must be confidential |
| FC03 | PKCE S256 | PKCE required with S256 challenge method only |
| FC04 | PAR Required | PAR must be required |
| FC05 | Sender-Constrained Tokens | DPoP or mTLS required |
| FC06 | Secure Client Auth | private_key_jwt or mTLS required |
| FC07 | Auth Code Lifetime | Authorization code lifetime ≤60 seconds |
| FC08 | Refresh Token Rotation Discouraged | Reusable refresh tokens preferred; rotation produces a warning |
| FC09 | DPoP Nonce | Informational: reports whether a DPoP-bound client uses server-provided nonces. Nonces are optional for the authorization server (§5.3.2.1), so this rule never fails. |
| FC10 | Explicit Redirect URIs | No wildcard (`*`) in the redirect URI host. IdentityServer matches redirect URIs by exact string comparison, so a wildcard host can never match (RFC 9700 §4.1.3). A `*` elsewhere is matched literally. |
| FC13 | Redirect URI Scheme | No `http` redirect URIs except loopback (RFC 8252 §7.3), and loopback is accepted only if the server opts in to loopback redirection (in IdentityServer, `StrictRedirectUriValidatorAppAuth`). Warns when `localhost` is used instead of a loopback IP literal (RFC 8252 §8.3). Confidential clients may have no registered redirect URIs when `AllowUnregisteredPushedRedirectUris` and the PAR endpoint are enabled. |

## Development

### Prerequisites

- .NET 10 SDK

### Building

```bash
dotnet build conformance-report/src/ConformanceReport/ConformanceReport.csproj
```

### Running Tests

```bash
dotnet test conformance-report/test/ConformanceReport.Tests/ConformanceReport.Tests.csproj
```

## License

This product requires a valid Duende Software license. For license terms, see the `LICENSE` file in the root of this repository or visit https://duendesoftware.com for more information.
