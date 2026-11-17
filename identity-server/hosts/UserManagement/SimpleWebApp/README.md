# Simple Web Application Sample

This sample demonstrates a simple ASP.NET Core web application that uses OpenID Connect to authenticate users via the Persona OTP IdentityServer.

## Overview

This is a minimal web application that delegates authentication to an external IdentityServer (OtpIdentityServer). It demonstrates the client-side implementation of the OpenID Connect authorization code flow with PKCE.

## Features

- **OpenID Connect Authentication**: Uses the standard OIDC protocol
- **Authorization Code Flow with PKCE**: Secure authentication flow
- **Claims Display**: Shows all claims received from IdentityServer
- **Profile Page**: Protected page requiring authentication
- **Clean Separation**: Authentication is completely handled by IdentityServer

## Prerequisites

1. .NET 10 SDK
2. OtpIdentityServer running at `https://localhost:5001`

## Configuration

The application is pre-configured to work with the OtpIdentityServer sample:

- **Authority**: `https://localhost:5001`
- **Client ID**: `webapp`
- **Client Secret**: `secret`
- **Redirect URI**: `https://localhost:5002/signin-oidc`
- **Scopes**: `openid`, `profile`, `email`

## Running the Sample

1. Start the OtpIdentityServer first (see its README)
2. Run this application:
   ```bash
   cd SimpleWebApp
   dotnet run
   ```
3. Navigate to `https://localhost:5002`

## Application Flow

1. User accesses the application
2. User clicks "Login"
3. Application redirects to IdentityServer
4. User authenticates with OTP at IdentityServer
5. IdentityServer redirects back to application with authorization code
6. Application exchanges code for tokens
7. User is logged in with claims from IdentityServer

## Pages

- **Index**: Home page showing authentication status and claims
- **Login**: Triggers the OIDC authentication flow
- **Logout**: Signs out from both the application and IdentityServer
- **Profile**: Protected page showing user profile information

## Authentication Configuration

The application uses cookie-based session management locally, with OpenID Connect for authentication challenges:

```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(...)
.AddOpenIdConnect(...);
```

## Claims Mapping

The application receives claims from IdentityServer including:
- `sub`: Subject identifier (user ID)
- `name`: User's display name
- `email`: User's email address
- `email_verified`: Email verification status
- Standard OIDC claims

## Security Features

- **HTTPS Required**: All communication happens over HTTPS
- **Secure Cookies**: Session cookies use `__Host-` prefix
- **PKCE**: Proof Key for Code Exchange is enabled
- **Token Validation**: ID tokens are validated automatically
- **Token Storage**: Access and refresh tokens are securely stored

## Customization

To use this application with a different instance of IdentityServer:

1. Update the `Authority` in `Program.cs`
2. Update the `ClientId` and `ClientSecret`
3. Ensure the redirect URIs match on both client and server
4. Add or remove scopes as needed

## Related Projects

See the [OtpIdentityServer](../OtpIdentityServer) sample for the IdentityServer implementation that this application authenticates against.
