
# Backend for Frontend (BFF) Security Framework 
_Securing SPAs and Blazor WASM applications once and for all._

## Overview
Duende.BFF is a framework for building services that solve security and identity problems in browser based applications such as SPAs and Blazor WASM applications. It is used to create a backend host that is paired with a frontend application. This backend is called the Backend For Frontend (BFF) host, and is responsible for all of the OAuth and OIDC protocol interactions. Moving the protocol handling out of JavaScript provides important security benefits and works around changes in browser privacy rules that increasingly disrupt OAuth and OIDC protocol flows in browser based applications. The Duende.BFF library makes it easy to build and secure BFF hosts by providing [session and token management](https://docs.duendesoftware.com/identityserver/v7/bff/session/), [API endpoint protection](https://docs.duendesoftware.com/identityserver/v7/bff/apis/), and [logout notifications](https://docs.duendesoftware.com/identityserver/v7/bff/session/management/back-channel-logout/).

## Extensibility
Duende.BFF can be extended with:
- custom logic at the session management endpoints
- custom logic and configuration for HTTP forwarding to external API endpoints
- custom data storage for server-side sessions and access/refresh tokens

## Advanced Security Features
Duende.BFF supports a wide range of security scenarios for modern applications:
- Mutual TLS
- Proof-of-Possession
- JWT secured authorization requests
- JWT-based client authentication. 

## Getting Started
If you're ready to dive into development, check out our [Quickstart Tutorial](https://docs.duendesoftware.com/identityserver/v7/quickstarts/js_clients/js_with_backend/) for step-by-step guidance.

For more in-depth documentation, visit [our documentation portal](https://docs.duendesoftware.com).

## Licensing
Duende.BFF is source-available, but requires a paid [license](https://duendesoftware.com/products/bff) for production use.

- **Development and Testing**: You are free to use and explore the code for development, testing, or personal projects without a license.
- **Production**: A license is required for production environments. 
- **Free Community Edition**: A free Community Edition license is available for qualifying companies and non-profit organizations. Learn more [here](https://duendesoftware.com/products/communityedition).

## Reporting Issues and Getting Support
- For bug reports or feature requests, [use our developer community forum](https://github.com/DuendeSoftware/community).
- For security-related concerns, please contact us privately at: **security@duendesoftware.com**.

## Related Packages
- [Duende.Bff.Yarp](https://www.nuget.org/packages/Duende.Bff.Yarp) - BFF integration with YARP (Yet Another Reverse Proxy)
- [Duende.Bff.EntityFramework](https://www.nuget.org/packages/Duende.Bff.EntityFramework) - A store for Duende.BFF's server side sessions implemented with Entity Framework
