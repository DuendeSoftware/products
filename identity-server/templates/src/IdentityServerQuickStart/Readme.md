# Duende IdentityServer Quickstart

Welcome to the Duende IdentityServer quickstart, a template designed to help you quickly get up and running with an OpenID Connect and OAuth 2.0 provider (OP). Please take the time to read this document to get a general overview of what the template offers and what proposed changes you can apply to make it your own.

We encourage you to modify this template to fit your needs, whether for local development, staging environments, or production deployments.

## Template Information

Here are some noteworthy attributes of this template that you should consider:

### .NET 9+

The template targets .NET 9, so you will need the latest .NET 9 SDK, which you can get from [dot.net](https://dotnet.microsoft.com/en-us/download)

### EntityFramework Core 9 SQLite

The template uses EntityFramework Core 9 with an SQLite database. The SQLite database is only meant for development purposes, and you should swap it for another relational database implementation, such as SQL Server, PostgreSQL, or MySQL/MariaDB. SQLite uses on-disk files to store data, and files of `.db`, `.db-shm`, and `.db-wal` are ignored from version control by default.

### In-Memory Users

This template utilizes and in-memory user store, and you can find these users in `TestUsers.cs`. This is fine for testing purposes, but you may want to consider storing users in a database at some point by implementing [`IProfileService`](https://docs.duendesoftware.com/identityserver/reference/services/profile-service/#duendeidentityserverservicesiprofileservice) or using [ASP.NET Identity](https://docs.duendesoftware.com/identityserver/aspnet-identity/) as a user storage mechanism.

### CSS Style Assets

This template used [Bootstrap 5](https://getbootstrap.com/) and Sass files. While we provide Sass files, we do not offer
build tooling to recompile them in this solution. You can build these files using built-in IDE tooling or command-line
tools such as [Vite](https://vitejs.dev/), [Webpack](https://webpack.js.org/), or [Gulp](https://gulpjs.com/).

## Getting Started

To run this application, you only need to build and run the application.

```bash
dotnet run --project <ProjectName>
```

The application will seed some initial data, which can be found in `Program.cs`.

```csharp
// this seeding is only for the template to bootstrap the DB and users.
// in production you will likely want a different approach.
Log.Information("Seeding database...");
SeedData.EnsureSeedData(app);
Log.Information("Done seeding database.");
```

After launching the web application, you can log in using the following credentials:

| User  | Password |
|-------|----------|
| admin | admin    |
| alice | alice    |
| bob   | bob      |

You can find these users in the `TestUsers` file. We highly recommend you log in as `admin` to configure clients, scopes, and claims.

## Documentation

To read more about Duende IdentityServer, we recommend you visit our [official documentation](https://docs.duendesoftware.com). There, you can learn about security topics and how to implement them in your ASP.NET Core solutions.

You can also get a jump start on your Duende IdentityServer knowledge by [completing our Quickstart series](https://docs.duendesoftware.com/identityserver/quickstarts/0-overview/) where you'll learn the ins and outs of implementing your custom identity provider.

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
