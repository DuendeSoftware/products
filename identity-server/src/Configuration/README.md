# Duende.IdentityServer.Configuration

## Overview
Duende.IdentityServer.Configuration provides a collection of endpoints that allow for management and configuration of an IdentityServer implementation using the Dynamic Client Registration [protocol](https://datatracker.ietf.org/doc/html/rfc7591). The Configuration API can be hosted either separately or within an IdentityServer implementation. 

This package includes abstractions for interacting with the IdentityServer configuration data store. You can either implement the store yourself or use [Duende.IdentityServer.Configuration.EntityFramework](https://www.nuget.org/packages/Duende.IdentityServer.Configuration.EntityFramework) for our default store implementation built with Entity Framework.

## Getting Started
A guide to installing and hosting the Configuration API is available [here](https://docs.duendesoftware.com/identityserver/configuration/dcr#installation-and-hosting). More [documentation](https://docs.duendesoftware.com/identityserver/configuration) and an [API reference](https://docs.duendesoftware.com/identityserver/reference/dcr/models/) are also available.

## Licensing
Duende IdentityServer is source-available, but requires a paid [license](https://duendesoftware.com/products/identityserver) for production use.

- **Development and Testing**: You are free to use and explore the code for development, testing, or personal projects without a license.
- **Production**: A license is required for production environments. 
- **Free Community Edition**: A free Community Edition license is available for qualifying companies and non-profit organizations. Learn more [here](https://duendesoftware.com/products/communityedition).

## Reporting Issues and Getting Support
- For bug reports or feature requests, [use our developer community forum](https://duende.link/community).
- For security-related concerns, please contact us privately at: **security@duendesoftware.com**.

## Related Packages
- [Duende.IdentityServer](https://www.nuget.org/packages/Duende.IdentityServer): OAuth and OpenID Connect framework with in-memory or customizable persistence.
- [Duende.IdentityServer.EntityFramework](https://www.nuget.org/packages/Duende.IdentityServer.EntityFramework.Storage): OAuth and OpenId Connect framework with Entity Framework based persistence.
- [Duende.IdentityServer.AspNetIdentity](https://www.nuget.org/packages/Duende.IdentityServer.AspNetIdentity): Integration between ASP.NET Core Identity and IdentityServer.
- [Duende.IdentityServer.Configuration.EntityFramework](https://www.nuget.org/packages/Duende.IdentityServer.Configuration.EntityFramework): Configuration API for IdentityServer with Entity Framework based persistence.
- [Duende.IdentityServer.Storage](https://www.nuget.org/packages/Duende.IdentityServer.Storage): Support package containing models and interfaces for the persistence layer of IdentityServer.
- [Duende.IdentityServer.EntityFramework.Storage](https://www.nuget.org/packages/Duende.IdentityServer.EntityFramework.Storage): Support package containing an implementation of the persistence layer of IdentityServer implemented with Entity Framework.
