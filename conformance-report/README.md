# Duende Conformance Report

_Standalone conformance assessment for OAuth 2.1 and FAPI 2.0 Security Profile compliance._

## Overview

Duende Conformance Report evaluates your IdentityServer configuration against OAuth 2.1 and FAPI 2.0 Security Profile requirements and generates an HTML report showing server and client configuration conformance.

For installation and setup instructions, see the [Duende.IdentityServer.ConformanceReport](https://www.nuget.org/packages/Duende.IdentityServer.ConformanceReport) package.

## Conformance Profiles

### OAuth 2.1

OAuth 2.1 consolidates best practices from OAuth 2.0, including mandatory PKCE, removal of deprecated grant types, and enhanced security requirements.

**Specification**: https://datatracker.ietf.org/doc/html/draft-ietf-oauth-v2-1-14

#### Server Rules

| Rule | Name | Requirement |
|------|------|-------------|
| S01 | PKCE Support | Server must support PKCE |
| S02 | Password Grant Prohibition | Assessed at client level |
| S03 | PAR Availability | PAR endpoint should be enabled |
| S04 | Sender-Constrained Token Support | mTLS or DPoP should be available |
| S05 | Secure Signing Algorithms | No symmetric or insecure algorithms |
| S06 | JWT Clock Skew | Clock skew within 5 minutes recommended |
| S07 | DPoP Nonce Support | Server should support DPoP nonce validation |
| S08 | HTTP 303 Redirects | Use HTTP 303 to prevent POST resubmission |

#### Client Rules

| Rule | Name | Requirement |
|------|------|-------------|
| C01 | Grant Types | Only authorization_code, client_credentials, refresh_token allowed |
| C02 | PKCE Required | PKCE must be required |
| C03 | No Plain Text PKCE | Plain text PKCE must be disabled |
| C04 | Explicit Redirect URIs | No wildcard redirect URIs |
| C05 | Client Authentication | Confidential clients must require authentication |
| C06 | PAR Required | PAR recommended for enhanced security |
| C07 | Sender-Constrained Tokens | DPoP or mTLS recommended |
| C08 | Auth Code Lifetime | Authorization code lifetime ≤60 seconds |
| C09 | Refresh Token Rotation | Refresh tokens should use one-time-only usage |
| C10 | DPoP Nonce | DPoP nonce validation required if DPoP enabled |
| C11 | Secure Client Authentication | Use private_key_jwt or mTLS |
| C12 | Refresh Token Support | Authorization code clients should support refresh tokens |

### FAPI 2.0 Security Profile

FAPI 2.0 Security Profile defines security requirements for high-risk scenarios such as financial services, requiring stronger authentication, authorization, and token security.

**Specification**: https://openid.net/specs/fapi-security-profile-2_0-final.html

#### Server Rules

| Rule | Name | Requirement |
|------|------|-------------|
| FS01 | PAR Required | PAR must be enabled and required |
| FS02 | Sender-Constrained Requirement | mTLS must be enabled |
| FS03 | Signing Algorithms | Only PS256/PS384/PS512 or ES256/ES384/ES512 allowed |
| FS04 | PAR Lifetime | PAR lifetime ≤600 seconds |
| FS05 | Token Mechanisms | mTLS or DPoP must be available |
| FS06 | Issuer Identification | Issuer identification response parameter required |
| FS07 | HTTP 303 Redirects | HTTP 303 redirects required |
| FS08 | PKCE Support | Server must support PKCE |

#### Client Rules

| Rule | Name | Requirement |
|------|------|-------------|
| FC01 | Grant Types | Only authorization_code and client_credentials allowed |
| FC02 | Confidential Client | Authorization code clients must be confidential |
| FC03 | PKCE S256 | PKCE required with S256 challenge method only |
| FC04 | PAR Required | PAR must be required |
| FC05 | Sender-Constrained Tokens | DPoP or mTLS required |
| FC06 | Secure Client Auth | private_key_jwt or mTLS required |
| FC07 | Auth Code Lifetime | Authorization code lifetime ≤60 seconds |
| FC08 | Refresh Token Rotation | Refresh token rotation required if refresh tokens enabled |
| FC09 | DPoP Nonce | DPoP nonce validation required if DPoP used |
| FC10 | Explicit Redirect URIs | No wildcard redirect URIs |
| FC11 | No Browser Tokens | Access tokens via browser must be disabled |
| FC12 | Request Object | Request object or PAR required |

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
