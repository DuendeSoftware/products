# Duende.BFF.Blazor

[![NuGet](https://img.shields.io/nuget/v/Duende.BFF.Blazor.svg)](https://www.nuget.org/packages/BFF.Blazor)

`Duende.BFF.Blazor` is a specialized .NET library designed to support secure browser-based applications, mainly using the Backend for Frontend (BFF) pattern in combination with Blazor Server, Blazor WASM (WebAssembly), and SPA frontends. 

It facilitates simpler and safer implementations of OpenID Connect (OIDC) and OAuth2 flows by moving token handling and other protocol interactions to the server.

[Learn more about the BFF pattern](https://docs.duendesoftware.com/bff/).

---

## Features

- **Token Management**: Provides server-side storage and handling of access tokens, improving security by removing tokens from the browser.
- **Seamless Integration With Blazor**: Easily integrates with Blazor Server or Blazor WASM applications, streamlining authentication functionalities.
- **Customizable Security**: Includes extension points to customize claims transformation, authentication logic, and session management.
- **Supports Modern OAuth2 and OIDC Flows**: Works around browser privacy restrictions affecting OAuth/OIDC protocols.
- **Proxies for Secure API Access**: Enables secure API calls via the server to prevent token exposure to the client.

---

## Getting Started

To get started with **Duende.BFF.Blazor**, follow these steps:

### Installation via NuGet

You can install the library via NuGet:

```bash
dotnet add package Duende.BFF.Blazor
```

### Dependencies

Ensure your project targets **.NET 8.0 or higher** and references ASP.NET Core for Blazor development. 

This library also integrates seamlessly with [Duende IdentityServer](https://duendesoftware.com/products/identityserver) or other compliant providers for OIDC and OAuth2.

---

## Usage

### Quick Example

1. **Configure Services**
   Add the necessary services in your `Startup.cs` or `Program.cs`:

   ```csharp
   builder.Services.AddBff()
       .AddBlazorBffServer()
       .AddServerSideManagementClaims();
   ```

2. **Update Middleware Pipeline**
   Update your app's middleware pipeline to include BFF features:

   ```csharp
   app.UseRouting();
   app.UseAuthentication();
   app.UseBff();
   app.UseAuthorization();

   app.MapBffManagementApis();
   app.MapControllers();
   ```

3. **Secure API Endpoints**
   Secure your API endpoints using the `[Authorize]` attribute to ensure they adhere to authentication and authorization policies.

   ```csharp
   [Authorize]
   [HttpGet("/api/secure-data")]
   public IActionResult GetSecureData()
   {
       return Ok(new { Message = "Secure data accessed" });
   }
   ```

4. **Integrate with Blazor Components**
   Use `AuthenticationStateProvider` or other related services to manage authentication state within your Blazor components.

## Documentation

Extensive documentation is available to guide you through key concepts, setup details, and advanced configuration options:

- [API Documentation](https://docs.duendesoftware.com/bff/fundamentals/)
- [Blazor Integration Guide](https://docs.duendesoftware.com/bff/fundamentals/blazor/)

## Related Projects

- [Duende.IdentityServer](https://github.com/DuendeSoftware/products) - Standards-compliant OpenID Connect and OAuth 2.0 framework.
- [BFF.YARP](https://www.nuget.org/packages/Duende.BFF.Yarp) - BFF integration with YARP for reverse proxying.

## Licensing

**BFF.Blazor** is source-available, but requires a paid license for production use:

- **Development and Testing**: Free to use and explore for personal or development purposes.
- **Production Use**: A commercial license is required. Visit [Duende Licensing](https://duendesoftware.com/license) for details.
- **Community Edition**: A free Community Edition license is available for qualifying organizations. Learn more [here](https://duendesoftware.com/products/communityedition).

By using **Duende.BFF.Blazor**, you agree to abide by its [licensing terms](https://duendesoftware.com/license).

## Contributing

We welcome community contributions. Please refer to our [contributing guidelines](https://github.com/YourGitHub/bff-blazor/blob/main/CONTRIBUTING.md) for more information.

## Support and Issues

- **Report Issues**: Use [GitHub Issues](https://github.com/duendesoftware/products/issues) for bugs and feature requests.
- **Security Concerns**: For security-related inquiries, contact **security@yourdomain.com**.
- **Community Discussions**: Join our [developer forum](https://github.com/duendesoftware/community).

