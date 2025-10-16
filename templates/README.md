# Duende.Templates
This package contains templates for Duende products for use with the .NET CLI (`dotnet new`).

## Installation

Install the templates with:

```
dotnet new install Duende.Templates
```

Note that you may have a previous version of Duende's templates (Duende.IdentityServer.Templates) installed on your machine. If the old template package is installed, we recommend uninstalling it with:

```
dotnet new uninstall Duende.Templates.IdentityServer
```

Or, for a more drastic complete reset of all templates back to the "factory defaults":

```
dotnet new --debug:reinit
```

## Identity Server Templates

### dotnet new duende-is-empty
Creates a minimal Duende IdentityServer host without a UI.

### dotnet new duende-is-ui
Adds the quickstart user interface to an IdentityServer host.

### dotnet new duende-is-inmem
Creates a basic Duende IdentityServer host with UI, test users and sample clients and resources, using in-memory operational and configuration stores.

### dotnet new duende-is-asp-id
Creates a basic Duende IdentityServer host that uses ASP.NET Identity for user management. If you automatically seed the database, you will get two users: `alice` and `bob` - both with password `Pass123$`. The `SeedData.cs` file controls the initial users.

### dotnet new duende-is-ef
Creates a basic Duende IdentityServer host that uses Entity Framework for configuration and state management. If you seed the database, you will get a couple of basic client and resource registrations. The `SeedData.cs` file controls the initial configuration data.

### dotnet new duende-is
The Duende IdentityServer template is our most feature-rich offering and a great starting point for developers who want a simple yet effective UI/UX experience.

The template is built on the Entity Framework Core template but provides an administrative UI for managing clients, scopes, and claims against a database storage engine.
It also has a diagnostics dashboard showing system information, including the licensing tier and features currently used in your IdentityServer deployment.


# BFF Templates

### dotnet new duende-bff-remoteapi
Creates a basic JavaScript-based BFF host that configures and invokes a remote API via the BFF proxy.

### dotnet new duende-bff-localapi
Creates a basic JavaScript-based BFF host that invokes a local API co-hosted with the BFF.

### dotnet new duende-bff-blazor
Creates a Blazor application that uses the interactive auto render mode, and secures the application across all render modes consistently using Duende.BFF.Blazor.

