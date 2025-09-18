// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Hosts.ServiceDefaults;

public static class AppHostServices
{
    public const string IdentityServer = "identity-server";
    public const string Api = "api";
    public const string IsolatedApi = "api-isolated";
    public const string Bff = "bff";
    public const string BffEf = "bff-ef";
    public const string BffBlazorWebassembly = "bff-blazor-webassembly";
    public const string BffBlazorPerComponent = "bff-blazor-per-component";
    public const string ApiDpop = "api-dpop";
    public const string BffDpop = "bff-dpop";
    public const string Migrations = "migrations";

    public static string[] All => [
        IdentityServer,
        Api,
        IsolatedApi,
        Bff,
        BffEf,
        BffBlazorWebassembly,
        BffBlazorPerComponent,
        ApiDpop,
        BffDpop,
        Migrations
    ];

}
