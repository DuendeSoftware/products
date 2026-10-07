# Securing SPAs and Blazor WASM applications once and for all

see [here](https://docs.duendesoftware.com/identityserver/v5/bff/) for documentation.

## Upgrading

### From v2.2.0 => v2.2.1

#### The Cookie request header is now removed by default on all YARP routes

This is a breaking change to a default in a patch release. It makes the YARP integration behave the same as the direct forwarder (`MapRemoteBffApiEndpoint`), which already removed the `Cookie` header.

Previously, when using `services.AddReverseProxy().AddBffExtensions()` with `MapReverseProxy`, the inbound `Cookie` header (including the BFF authentication cookie) was forwarded unchanged to upstream APIs on every route, with or without `Duende.Bff.Yarp.TokenType` or optional user token metadata. Routes configured with an access token (for example `WithAccessToken(...)` or `WithOptionalUserAccessToken()`) forwarded the cookie as well.

Now, the `Cookie` request header is removed by default from every route proxied through the BFF YARP integration, with or without token metadata. This is controlled by `BffOptions.RemoveCookieHeaderFromYarpRequests`, which defaults to `true`.

If you relied on the previous behavior, set `RemoveCookieHeaderFromYarpRequests` to `false`, for example with `services.Configure<BffOptions>(o => o.RemoveCookieHeaderFromYarpRequests = false)` or in `AddBff(o => ...)`. This setting applies to every YARP route: turning it off forwards the browser's cookies, including the BFF authentication cookie, to every upstream API. Remove the header yourself on each route that doesn't need cookies, using a standard YARP transform such as `.AddTransforms(ctx => ctx.AddRequestHeaderRemove("Cookie"))` or a configuration-based `RequestHeaderRemove: Cookie` transform.

The `Cookie` header is removed after the request headers are copied and after the transforms configured on the route. Transform providers or transforms registered after calling `AddBffExtensions` (for example additional calls to `.AddTransforms(...)` on the `IReverseProxyBuilder` returned by `AddBffExtensions`) run later and can still modify the request, including setting a `Cookie` header.