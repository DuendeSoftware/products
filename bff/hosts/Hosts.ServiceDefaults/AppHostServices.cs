// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Hosts.ServiceDefaults;

public static class AppHostServices
{
    public const string BffPerf = "bff-perf";
    public const string IdentityServer = "identity-server";
    public const string Api = "api";
    public const string IsolatedApi = "api-isolated";
    public const string Bff = "bff";
    public const string BffMultiFrontend = "bff-multi-frontend";
    public const string BffEf = "bff-ef";
    public const string BffBlazorWebassembly = "bff-blazor-webassembly";
    public const string BffBlazorPerComponent = "bff-blazor-per-component";
    public const string ApiDpop = "api-dpop";
    public const string BffDpop = "bff-dpop";
    public const string Migrations = "migrations";
    public const string TemplateBffBlazor = "template-bff-blazor";
    public const string TemplateBffLocal = "templates-bff-local";
    public const string TemplateBffRemote = "templates-bff-remote";

    public static string[] All => [
        IdentityServer,
        Api,
        IsolatedApi,
        Bff,
        BffPerf,
        BffEf,
        BffBlazorWebassembly,
        BffBlazorPerComponent,
        ApiDpop,
        BffDpop,
        Migrations,
        TemplateBffBlazor,
        TemplateBffLocal,
        TemplateBffRemote
    ];

}

public static class ServiceDiscovery
{
    public static Uri ResolveService(string serviceName, string appName = "https")
    {
        var host = serviceName;

        // Compose the environment variable key
        var envVarKey = $"services__{host}__{appName}__0";

        // Try to get the value from environment variables
        var value = Environment.GetEnvironmentVariable(envVarKey);

        if (string.IsNullOrEmpty(value))
        {
            throw new InvalidOperationException($"Service endpoint for '{serviceName}' not found in environment variable '{envVarKey}'.");
        }

        return new Uri(value);
    }
}
