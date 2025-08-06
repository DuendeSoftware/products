# Duende IdentityServer

Welcome to your new Duende IdentityServer! This template is designed to help you quickly get up and running with a secure OpenID Connect and OAuth 2.0 provider.

This document provides an overview of the template and how to customize it for your own needs, whether for local development, staging, or production.

## Key Features

Here are some key features of this template:

### .NET 9+

This template targets .NET 9. You can get the latest SDK from [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download)

### EntityFramework Core 9 with SQLite

This template uses our [EntityFramework Store](https://docs.duendesoftware.com/identityserver/data/ef) to persist configuration and operational data.

For simplicity, it's pre-configured with SQLite. This is great for development and testing, but *it is not recommended for production*. We strongly advise switching to a more robust relational database like SQL Server, PostgreSQL, or MySQL for production environments.

### In-Memory Test Users
For demonstration purposes, we've included an in-memory user store in TestUsers.cs. This approach is *for development only and should not be used in production.*

For a production environment, you should replace this with your own user database or integrate with [ASP.NET Identity](https://docs.duendesoftware.com/identityserver/aspnet-identity/). To do that, you'll need to
1. Update the login UI
   Replace the `TestUserStore` in the UI with logic that validates users against your store.
2. Provide an implementation of `IProfileService`
   This service is used to populate claims in tokens and the userinfo endpoint.
   - For custom user stores: You will write you own implementation.
   - For ASP.NET Identity: You can use the built-in implementation provided by our ASP.NET Identity integration package.

### CSS Style Assets

The user interface is styled with [Bootstrap 5](https://getbootstrap.com/). We include the source Sass files, but this template does not provide a build tool to recompile them. To customize the styles, you'll need to recompile the Sass files using your preferred tool, such as your IDE's built-in features or a command-line tool like [Vite](https://vitejs.dev/), [Webpack](https://webpack.js.org/), or [Gulp](https://gulpjs.com/).

## Getting Started

To get started, just run the project:

```bash
dotnet run --project <ProjectName>
```

On first launch, the application will seed the database with initial configuration data and test users. This logic is located in SeedData.cs and is called from Program.cs.

```csharp
// this seeding is only for the template to bootstrap the DB and users.
// in production you will likely want a different approach.
Log.Information("Seeding database...");
SeedData.EnsureSeedData(app);
Log.Information("Done seeding database.");
```

You can now log in with one of the pre-configured test users found in the `TestUsers.cs` file:

| User  | Password |
|-------|----------|
| admin | admin    |
| alice | alice    |
| bob   | bob      |

We recommend logging in as `admin` to explore the administration UI, where you can configure clients, scopes, and claims.

## Documentation

To read more about Duende IdentityServer, visit our [official documentation](https://docs.duendesoftware.com). There, you can learn about security topics and how to implement them in your ASP.NET Core solutions.

You can also get a jump start on your Duende IdentityServer knowledge by [completing our Quickstart series](https://docs.duendesoftware.com/identityserver/quickstarts/0-overview/) where you'll learn the ins and outs of implementing your own identity provider.

## License

MIT License

Copyright (c) 2025 Duende Software

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
