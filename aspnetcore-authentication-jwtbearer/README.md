# Duende JWT Bearer Authentication Extensions

Extensions for
the [ASP.NET JwtBearer authentication handler](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.JwtBearer)
that add support for advanced features of Duende IdentityServer, particularly OAuth 2.0 Demonstrating
Proof-of-Possession (DPoP) as specified in [RFC 9449](https://datatracker.ietf.org/doc/rfc9449/).

Read more about the Duende packages at [documentation](https://docs.duendesoftware.com/).

## Features

* Implements DPoP support for enhanced security of bearer tokens
* Seamlessly integrates with existing ASP.NET Core JWT Bearer authentication
* Validates DPoP proofs according to RFC 9449 specifications

The following services collection extension methods are included under the features of this package:

1. **`ConfigureDPoPTokensForScheme(this IServiceCollection services, string scheme)`**
   Configures DPoP support for a specific JwtBearer authentication scheme.

2. **`ConfigureDPoPTokensForScheme(this IServiceCollection services, string scheme, Action<DPoPOptions> configure)`**
   Configures DPoP support for a specific JwtBearer authentication scheme and allows customization through `DPoPOptions`.

#### HTTP Request Extensions

- **`GetAuthorizationScheme(this HttpRequest request)`**: Retrieves the authorization scheme from the `Authorization` header of the HTTP request.
- **`GetDPoPProofToken(this HttpRequest request)`**: Retrieves the DPoP proof token from the `DPoP` header of the HTTP request.

#### Authentication Properties Extensions

- **`GetDPoPNonce(this AuthenticationProperties props)`**: Retrieves the nonce value used for DPoP from the provided `AuthenticationProperties`.
- **`SetDPoPNonce(this AuthenticationProperties props, string nonce)`**: Sets a nonce value used for DPoP in the provided `AuthenticationProperties`.

#### JSON Web Key Extensions

- **`CreateThumbprintCnf(this JsonWebKey jwk)`**: Creates the value of a confirmation claim (`cnf`) from a JSON Web Key thumbprint.
- **`CreateThumbprint(this JsonWebKey jwk)`**: Creates the thumbprint of a JSON Web Key (`jwk`).


## Licensing

Duende IdentityServer is source-available, but requires a paid license for production use.

- **Development and Testing**: You are free to use and explore the code for development, testing, or personal projects without a license.
- **Production**: A license is required for production environments.
- **Free Community Edition**: A free Community Edition license is available for qualifying companies and non-profit organizations.

By accessing the Duende Products code here, you are agreeing to the [licensing terms](https://duendesoftware.com/license).

## Contributing

Please see our [contributing guidelines](https://github.com/DuendeSoftware/products/blob/main/.github/CONTRIBUTING.md).

## Reporting Issues and Getting Support

- For bug reports or feature requests, [use our developer community forum](https://github.com/DuendeSoftware/community).
- For security-related concerns, please contact us privately at: **security@duendesoftware.com**.
