# Duende IdentityServer
_The most flexible and standards-compliant OpenID Connect and OAuth 2.x framework for ASP.NET Core._

Welcome to the official GitHub repository for [Duende](https://duendesoftware.com) IdentityServer!

## Overview

Duende IdentityServer is a highly extensible, standards-compliant framework for implementing the OpenID Connect and OAuth 2.x protocols in ASP.NET Core. It offers deep flexibility for handling authentication, authorization, and token issuance and can be adapted to fit complex custom security scenarios.

### Extensibility Points

- **Customizable User Experience**: Go beyond simple branding to fully customizable user interfaces.
- **Core Engine Customization**: The engine itself is modular and built from services that can be extended or overridden.

### Advanced Security Scenarios

Duende IdentityServer supports a wide range of security scenarios for modern applications:

- **Federation**: Easily integrate with external identity providers or other authentication services using [federation](https://docs.duendesoftware.com/identityserver/ui/federation/).
- **Token Exchange**: Enable secure token exchange between clients and services with [Token Exchange](https://docs.duendesoftware.com/identityserver/tokens/).
- **Audience Constrained Tokens**: Restrict tokens to specific audiences, increasing security in multi-service architectures. Learn more about [audience-constrained tokens](https://docs.duendesoftware.com/identityserver/fundamentals/resources/isolation/).
- **Sender Constrained Tokens**: Implement Proof of Possession (PoP) tokens with [DPoP or mTLS](https://docs.duendesoftware.com/identityserver/tokens/pop/), which bind tokens to the client, adding another layer of protection.
- **Pushed Authorization Requests (PAR)**: Support [Pushed Authorization Requests](https://docs.duendesoftware.com/identityserver/tokens/par/) to enhance the security of the authorization flow.

## Getting Started
If you're ready to dive into development, check out our [Quickstart Tutorial Series](https://docs.duendesoftware.com/identityserver/quickstarts/0-overview/) for step-by-step guidance.

For more in-depth documentation, visit [our documentation portal](https://docs.duendesoftware.com).

## Licensing
Duende IdentityServer is source-available, but requires a paid [license](https://duendesoftware.com/products/identityserver) for production use.

- **Development and Testing**: You are free to use and explore the code for development, testing, or personal projects without a license.
- **Production**: A license is required for production environments. 
- **Free Community Edition**: A free Community Edition license is available for qualifying companies and non-profit organizations. Learn more [here](https://duendesoftware.com/products/communityedition).

## Reporting Issues and Getting Support
- For bug reports or feature requests, [use our developer community forum](https://duende.link/community).
- For security-related concerns, please contact us privately at: **security@duendesoftware.com**.
