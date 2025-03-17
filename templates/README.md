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
