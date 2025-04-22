# Duende.Templates.IdentityServer
.NET CLI Templates for Duende IdentityServer

### dotnet new is-empty
Creates a minimal Duende IdentityServer project without a UI.

### dotnet new is-inmem
Adds a basic Duende IdentityServer with UI, test users and sample clients and resources.

### dotnet new is-asp-id
Adds a basic Duende IdentityServer that uses ASP.NET Identity for user management. If you automatically seed the database, you will get two users: `alice` and `bob` - both with password `Pass123$`. Check the `SeedData.cs` file.

### dotnet new is-ef
Adds a basic Duende IdentityServer that uses Entity Framework for configuration and state management. If you seed the database, you get a couple of basic client and resource registrations, check the `SeedData.cs` file.

## Installation

Install with:

`dotnet new install Duende.Templates.IdentityServer`


If you need to set back your dotnet new list to "factory defaults", use this command:

`dotnet new --debug:reinit`

If you find that this doesn't work, remove the entries from the file %userprofile%/.templateengine/packages.json

To uninstall the templates, use

`dotnet new uninstall Duende.Templates.IdentityServer`

# Duende.Templates.BFF
.NET CLI Templates for Duende BFF

## Building

``` pwsh
cd templates
dotnet run -p build

dotnet pack ..\artifacts\ -o d:\packages

```

### dotnet new duende-bff-remoteapi
Creates a basic JavaScript-based BFF host that configures and invokes a remote API via the BFF proxy.

### dotnet new duende-bff-localapi
Creates a basic JavaScript-based BFF host that invokes a local API co-hosted with the BFF.

## Installation 

Install with:

`dotnet new install Duende.Templates`


If you need to set back your dotnet new list to "factory defaults", use this command:

`dotnet new --debug:reinit`

If you find that this doesn't work, remove the entries from the file %userprofile%/.templateengine/packages.json

To uninstall the templates, use 

`dotnet new uninstall Duende.Templates`
