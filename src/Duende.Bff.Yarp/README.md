
# Backend for Frontend (BFF) YARP Integration
_Securing SPAs and Blazor WASM applications once and for all._

## Overview
Duende.BFF is a framework for building services that solve security and identity problems in browser based applications such as SPAs and Blazor WASM applications. It is used to create a backend host that is paired with a frontend application. This backend is called the Backend For Frontend (BFF) host, and is responsible for all of the OAuth and OIDC protocol interactions. Moving the protocol handling out of JavaScript provides important security benefits and works around changes in browser privacy rules that increasingly disrupt OAuth and OIDC protocol flows in browser based applications. The Duende.BFF library makes it easy to build and secure BFF hosts by providing [session and token management](https://docs.duendesoftware.com/identityserver/v7/bff/session/), [API endpoint protection](https://docs.duendesoftware.com/identityserver/v7/bff/apis/), and [logout notifications](https://docs.duendesoftware.com/identityserver/v7/bff/session/management/back-channel-logout/).

This package integrates the BFF with Microsoft's YARP (Yet Another Reverse Proxy). It allows you to proxy requests to external APIs using the full power of YARP, while also applying Duende.BFF's token management and security features.

## Getting Started
For in-depth documentation, please see the Duende.BFF.Yarp [documentation page](https://docs.duendesoftware.com/identityserver/v7/bff/apis/yarp/).

## Behavior change in 2.3.1: the Cookie request header is removed on all YARP routes
This changes a default in a patch release. It makes the YARP integration behave the same as the direct forwarder (`MapRemoteBffApiEndpoint`), which already removed the `Cookie` header.

Previously, when using `services.AddReverseProxy().AddBffExtensions()` with `MapReverseProxy`, the inbound `Cookie` header (including the BFF authentication cookie) was forwarded unchanged to upstream APIs on every route, with or without `Duende.Bff.Yarp.TokenType` metadata. Routes configured with an access token (for example `WithAccessToken(...)` or `WithOptionalUserAccessToken()`) forwarded the cookie as well.

Now, the `Cookie` request header is removed by default from every route proxied through the BFF YARP integration. This is controlled by `BffOptions.RemoveCookieHeaderFromYarpRequests`, which defaults to `true`.

If you relied on the previous behavior, set `RemoveCookieHeaderFromYarpRequests` to `false`, for example with `services.Configure<BffOptions>(o => o.RemoveCookieHeaderFromYarpRequests = false)` or in `AddBff(o => ...)`. This setting applies to every YARP route: turning it off forwards the browser's cookies, including the BFF authentication cookie, to every upstream API. Remove the header yourself on each route that doesn't need cookies, using a standard YARP transform such as `.AddTransforms(ctx => ctx.AddRequestHeaderRemove("Cookie"))` or a configuration-based `RequestHeaderRemove: Cookie` transform.

The `Cookie` header is removed after the request headers are copied and after the transforms configured on the route. Transform providers or transforms registered after calling `AddBffExtensions` (for example additional calls to `.AddTransforms(...)` on the `IReverseProxyBuilder`) run later and can still modify the request, including setting a `Cookie` header.

## Licensing
Duende.BFF.Yarp is source-available, but requires a paid [license](https://duendesoftware.com/products/bff) for production use.

- **Development and Testing**: You are free to use and explore the code for development, testing, or personal projects without a license.
- **Production**: A license is required for production environments. 
- **Free Community Edition**: A free Community Edition license is available for qualifying companies and non-profit organizations. Learn more [here](https://duendesoftware.com/products/communityedition).

## Reporting Issues and Getting Support
- For bug reports or feature requests, open an issue on GitHub: [Submit an Issue](https://github.com/DuendeSoftware/Support/issues/new/choose).
- For security-related concerns, please contact us privately at: **security@duendesoftware.com**.

## Related Packages
- [Duende.Bff](https://www.nuget.org/packages/Duende.Bff) - Framework for building browser based applications using the BFF pattern
- [Duende.Bff.EntityFramework](https://www.nuget.org/packages/Duende.Bff.EntityFramework) - A store for Duende.BFF's server side sessions implemented with Entity Framework
