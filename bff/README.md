
# Backend for Frontend (BFF) Security Framework 
_Securing SPAs and Blazor WASM applications once and for all._

Welcome to the official GitHub repository for the [Duende](https://duendesoftware.com) Backend for Frontend (BFF) Security Framework!

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

## Running the Hosts.AppHost project

The Hosts.AppHost project is an Aspnet Aspire project that launches all dependencies. For example, it starts an identity server and various ways
that the BFF can be configured. Use this to test if the functionality is still working. 

There's also an integration test project covering this. This project can run in 3 modes:

1. **Directly**. Then the test fixture will launch an aspire test host. It will run all tests against the aspire test host. 
2. **With manually run aspire host.** The advantage of this is that you can keep your aspire host running and only iterate on your tests. This is more efficient for writing the tests. 
It also leaves the door open to re-using these tests to run them against a deployed in stance somewhere in the future. Downside is that you cannot debug both your tests and host at the same time because visual studio compiles them in the same location.
3. **With NCrunch**. It turns out that NCrunch doesn't support building aspire projects. Iterating over the tests using ncrunch is the fastest way to get feedback. However, to make this work, conditional compilation is used. 


Starting the host can be done via the UI (set as startup project using 'HTTPS' as the launch profile). It can also be started from the command line (which makes iterating over the tests faster)
Running it with configuration release means you can compile / modify the tests while keeping the dev server running. 

``` powershell
dotnet run -p samples/Hosts.AppHost --Configuration Release
```

## Licensing
Duende.BFF is source-available, but requires a paid [license](https://duendesoftware.com/products/bff) for production use.

- **Development and Testing**: You are free to use and explore the code for development, testing, or personal projects without a license.
- **Production**: A license is required for production environments. 
- **Free Community Edition**: A free Community Edition license is available for qualifying companies and non-profit organizations. Learn more [here](https://duendesoftware.com/products/communityedition).

## Reporting Issues and Getting Support
- For bug reports or feature requests, [use our developer community forum](https://github.com/DuendeSoftware/community).
- For security-related concerns, please contact us privately at: **security@duendesoftware.com**.
