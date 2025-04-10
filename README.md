# Duende Products

[![License](https://img.shields.io/badge/License-Duende%20Software-blue)](https://duendesoftware.com/license)
[![GitHub Discussions](https://img.shields.io/github/discussions/DuendeSoftware/community)](https://github.com/orgs/DuendeSoftware/discussions)

This repository contains the core products developed by Duende Software.

### Duende IdentityServer
[![NuGet](https://img.shields.io/nuget/v/Duende.IdentityServer.svg)](https://www.nuget.org/packages/Duende.IdentityServer)
[![IdentityServer CI](https://github.com/DuendeSoftware/products/actions/workflows/identity-server-ci.yml/badge.svg)](https://github.com/DuendeSoftware/products/actions/workflows/identity-server-ci.yml)

Duende IdentityServer is a modern, standards-compliant OpenID Connect and OAuth 2.0 framework for ASP.NET Core, designed to provide secure authentication and API access control for modern applications. It supports a wide range of authentication flows, token types, and extension points for customization.

- [Documentation](https://docs.duendesoftware.com/identityserver/v7)
- [Source Code](./identity-server)

### Duende BFF (Backend for Frontend)

[![NuGet](https://img.shields.io/nuget/v/Duende.BFF.svg)](https://www.nuget.org/packages/Duende.BFF)
[![BFF CI](https://github.com/DuendeSoftware/products/actions/workflows/bff-ci.yml/badge.svg)](https://github.com/DuendeSoftware/products/actions/workflows/bff-ci.yml)

The Backend for Frontend (BFF) pattern is a security architecture for browser-based JavaScript applications. It keeps access and refresh tokens on the server and eliminates the need for CORS, providing improved security for your web applications.

- [Documentation](https://docs.duendesoftware.com/identityserver/v7/bff/)
- [Source Code](./bff)

### AspNet Core JWT Bearer Authentication Extensions

[![NuGet](https://img.shields.io/nuget/v/Duende.AspNetCore.Authentication.JwtBearer.svg)](https://www.nuget.org/packages/Duende.AspNetCore.Authentication.JwtBearer)
[![JwtBearer CI](https://github.com/DuendeSoftware/products/actions/workflows/aspnetcore-authentication-jwtbearer-ci.yml/badge.svg)](https://github.com/DuendeSoftware/products/actions/workflows/aspnetcore-authentication-jwtbearer-ci.yml)

Extends the ASP.NET Core JWT Bearer authentication handler with support for OAuth 2.0 Demonstrating Proof-of-Possession (DPoP), enhancing security for bearer tokens by proving possession of a private key.

- [Documentation](https://docs.duendesoftware.com/identityserver/v7/apis/aspnetcore/dpop/)
- [Source Code](./aspnetcore-authentication-jwtbearer)

## License

By accessing the Duende Products code here, you are agreeing to the [licensing terms](https://duendesoftware.com/license).

## Contributing

Please see our [contributing guidelines](/.github/CONTRIBUTING.md).