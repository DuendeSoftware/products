# Duende JwtBearer Extensions

## Overview

Duende.AspNetCore.Authentication.JwtBearer (JwtBearer Extensions) extends the [ASP.NET Core JwtBearer authentication handler](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer) with advanced protocol features, most notably support for [DPoP](https://datatracker.ietf.org/doc/rfc9449/). JwtBearer Extensions is an easy-to-use add-on for the JwtBearer authentication handler in ASP.NET Core. To get started, it requires nothing more than a single NuGet package and minimal configuration, but it also supports advanced protocol features like replay detection and server-issued nonces, allows configuring signing algorithms, clocks skews, etc., and enables extensibility.

## What is DPoP
DPoP is an OAuth security protocol that protects against one of the most common threats in the ecosystem: abuse of stolen tokens. Stolen access tokens can be abused easily because they are typically bearer tokens, meaning that any bearer, or holder, of the token can use it. DPoP prevents this abuse by sender-constraining tokens so that only the party that was issued a token can use it. This is accomplished by binding tokens to a public-private key pair in the possession of the client.

The client proves possession of the private key by signing a specialized JSON Web Token (JWT) called a DPoP Proof Token with the private key. Whenever the client wants to use its token, it must produce a new proof, because proofs are short-lived and specific to a particular endpoint. This makes a stolen access token unusable by an attacker who does not possess the private key.


## Getting Started

To get started, install this package and then add some minimal configuration:

```cs
// Keep your existing code that configures the JwtBearer handler unchanged:
builder.Services.AddAuthentication("token")
    .AddJwtBearer("token", options => { /* Your existing configuration here */ });

// Add DPoP support with our extensions:
builder.Services.ConfigureDPoPTokensForScheme("token", options =>
{
    options.EnableReplayDetection = false; // Disable replay detection to show a minimal setup
    options.AllowBearerTokens = true; // Allow both Bearer and DPoP tokens, to facilitate migration to DPoP
});
```

## Documentation
See [our documentation](https://docs.duendesoftware.com/identityserver/apis/aspnetcore/confirmation/#validating-dpop) for more information.

## Licensing
The Duende JwtBearer Extensions are source-available, but require a paid [license](https://duendesoftware.com/products/identityserver) for production use.

- **Development and Testing**: You are free to use and explore the code for development, testing, or personal projects without a license.
- **Production**: A license is required for production environments.
- **Free Community Edition**: A free Community Edition license is available for qualifying companies and non-profit organizations. Learn more [here](https://duendesoftware.com/products/communityedition).

## Reporting Issues and Getting Support
- For bug reports or feature requests, [use our developer community forum](https://duende.link/community).
- For security-related concerns, please contact us privately at: **security@duendesoftware.com**.
