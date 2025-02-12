# IdentityServer.Templates
.NET CLI Templates for Duende IdentityServer

### dotnet new isempty
Creates a minimal Duende IdentityServer project without a UI.

### dotnet new isui
Adds the quickstart UI to the current project (can be e.g added on top of *isempty*)

### dotnet new isinmem
Adds a basic Duende IdentityServer with UI, test users and sample clients and resources.

### dotnet new isaspid
Adds a basic Duende IdentityServer that uses ASP.NET Identity for user management. If you automatically seed the database, you will get two users: `alice` and `bob` - both with password `Pass123$`. Check the `SeedData.cs` file.

### dotnet new isef
Adds a basic Duende IdentityServer that uses Entity Framework for configuration and state management. If you seed the database, you get a couple of basic client and resource registrations, check the `SeedData.cs` file.

### dotnet new bff-remoteapi
Creates a basic JavaScript-based BFF host that configures and invokes a remote API via the BFF proxy.

### dotnet new bff-localapi
Creates a basic JavaScript-based BFF host that invokes a local API co-hosted with the BFF.

## Installation 

Install with:

`dotnet new install Duende.IdentityServer.Templates`


If you need to set back your dotnet new list to "factory defaults", use this command:

`dotnet new --debug:reinit`


To uninstall the templates, use 

`dotnet new uninstall Duende.IdentityServer.Templates`
