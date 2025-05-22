// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using aspire.orchestrator.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Create a collection of the project resources to be used by the orchestrator
// This will allow us to refer back to the projects when setting up references.
var projectRegistry = new Dictionary<string, IResourceBuilder<ProjectResource>>();

ConfigureIdentityServerHosts();
ConfigureApis();
ConfigureClients();

builder.Build().Run();


void ConfigureIdentityServerHosts()
{
    // These hosts don't require additional infrastructure
    if (HostIsEnabled(nameof(Projects.Host_Main)))
    {
        var hostMain = builder
            .AddProject<Projects.Host_Main>("is-host")
            .WithHttpsHealthCheck(path: "/.well-known/openid-configuration");

        projectRegistry.Add("is-host", hostMain);
    }
    if (HostIsEnabled(nameof(Projects.Host_Configuration)))
    {
        var hostConfiguration = builder
            .AddProject<Projects.Host_Configuration>("is-host")
            .WithHttpsHealthCheck(path: "/.well-known/openid-configuration");

        projectRegistry.Add("is-host", hostConfiguration);
    }

    // These hosts require a database
    var dbHosts = new List<string>
    {
        nameof(Projects.Host_AspNetIdentity),
        nameof(Projects.Host_EntityFramework),
        nameof(Projects.Host_EntityFramework_dotnet9)
    };

    if (dbHosts.Any(HostIsEnabled))
    {
        // Adds SQL Server to the builder (requires Docker).
        // Feel free to use your preferred docker management service.
        var sqlServer = builder
            .AddSqlServer(name: "SqlServer", port: 62949)
            .WithLifetime(ContainerLifetime.Persistent);

        var identityServerDb = sqlServer
            .AddDatabase(name: "IdentityServerDb", databaseName: "IdentityServer");

        if (HostIsEnabled(nameof(Projects.Host_AspNetIdentity)))
        {
            var aspnetMigration = builder.AddProject<Projects.AspNetIdentityDb>(name: "aspnetidentitydb-migrations")
                .WithReference(identityServerDb, connectionName: "DefaultConnection")
                .WaitFor(identityServerDb);

            var hostAspNetIdentity = builder.AddProject<Projects.Host_AspNetIdentity>(name: "is-host")
                .WithHttpsHealthCheck(path: "/.well-known/openid-configuration")
                .WithReference(identityServerDb, connectionName: "DefaultConnection")
                .WaitForCompletion(aspnetMigration);

            projectRegistry.Add("is-host", hostAspNetIdentity);
        }

        if (HostIsEnabled(nameof(Projects.Host_EntityFramework)))
        {
            var idSrvMigration = builder.AddProject<Projects.IdentityServerDb>(name: "identityserverdb-migrations")
                .WithReference(identityServerDb, connectionName: "DefaultConnection")
                .WaitFor(identityServerDb);

            var hostEntityFramework = builder.AddProject<Projects.Host_EntityFramework>(name: "is-host")
                .WithHttpsHealthCheck(path: "/.well-known/openid-configuration")
                .WithReference(identityServerDb, connectionName: "DefaultConnection")
                .WaitForCompletion(idSrvMigration);

            projectRegistry.Add("is-host", hostEntityFramework);
        }

        if (HostIsEnabled(nameof(Projects.Host_EntityFramework_dotnet9)))
        {
            var idSrvMigration = builder.AddProject<Projects.IdentityServerDb>(name: "identityserverdb-migrations")
                .WithReference(identityServerDb, connectionName: "DefaultConnection")
                .WaitFor(identityServerDb);

            var hostEntityFramework = builder.AddProject<Projects.Host_EntityFramework_dotnet9>(name: "is-host")
                .WithHttpsHealthCheck(path: "/.well-known/openid-configuration")
                .WithReference(identityServerDb, connectionName: "DefaultConnection")
                .WaitForCompletion(idSrvMigration);

            projectRegistry.Add("is-host", hostEntityFramework);
        }
    }

    bool HostIsEnabled(string name) => builder.Configuration
        .GetSection($"AspireProjectConfiguration:IdentityHost").Value?
        .Equals(name, StringComparison.OrdinalIgnoreCase) ?? false;
}

void ConfigureApis()
{
    if (ApiIsEnabled(nameof(Projects.SimpleApi)))
    {
        var simpleApi = builder.AddProject<Projects.SimpleApi>(name: "simple-api");
        projectRegistry.Add("simple-api", simpleApi);
    }

    if (ApiIsEnabled(nameof(Projects.ResourceBasedApi)))
    {
        var resourceBasedApi = builder.AddProject<Projects.ResourceBasedApi>(name: "resource-based-api");
        projectRegistry.Add("resource-based-api", resourceBasedApi);
    }

    if (ApiIsEnabled(nameof(Projects.DPoPApi)))
    {
        var dpopApi = builder.AddProject<Projects.DPoPApi>(name: "dpop-api");
        projectRegistry.Add("dpop-api", dpopApi);
    }

    bool ApiIsEnabled(string name) => builder.Configuration
        .GetSection($"AspireProjectConfiguration:UseApis:{name}").Value?
        .Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
}

void ConfigureClients()
{
    ConfigureWebClients();

    ConfigureConsoleClients();
}

void ConfigureWebClients()
{
    RegisterClientIfEnabled<Projects.MvcCode>("mvc-code");
    RegisterClientIfEnabled<Projects.MvcDPoP>("mvc-dpop");
    RegisterClientIfEnabled<Projects.JsOidc>("js-oidc");
    RegisterClientIfEnabled<Projects.MvcAutomaticTokenManagement>("mvc-automatic-token-management");
    RegisterClientIfEnabled<Projects.MvcHybridBackChannel>("mvc-hybrid-backchannel");
    RegisterClientIfEnabled<Projects.MvcJarJwt>("mvc-jar-jwt");
    RegisterClientIfEnabled<Projects.MvcJarUriJwt>("mvc-jar-uri-jwt");
}

void ConfigureConsoleClients()
{
    RegisterClientIfEnabled<Projects.ConsoleCibaClient>("console-ciba-client", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleDeviceFlow>("console-device-flow", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleClientCredentialsFlow>("console-client-credentials-flow", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleClientCredentialsFlowCallingIdentityServerApi>("console-client-credentials-flow-callingidentityserverapi", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleClientCredentialsFlowPostBody>("console-client-credentials-flow-postbody", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleClientCredentialsFlowDPoP>("console-client-credentials-flow-dpop", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleDcrClient>("console-dcr-client", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleEphemeralMtlsClient>("console-ephemeral-mtls-client", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleExtensionGrant>("console-extension-grant", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleIntrospectionClient>("console-introspection-client", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleMTLSClient>("console-mtls-client", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsolePrivateKeyJwtClient>("console-private-key-jwt-client", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleResourceOwnerFlow>("console-resource-owner-flow", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleResourceOwnerFlowPublic>("console-resource-owner-flow-public", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleResourceOwnerFlowReference>("console-resource-owner-flow-reference", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleResourceOwnerFlowRefreshToken>("console-resource-owner-flow-refresh-token", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleResourceOwnerFlowUserInfo>("console-resource-owner-flow-userinfo", explicitStart: true);
    RegisterClientIfEnabled<Projects.WindowsConsoleSystemBrowser>("console-system-browser", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleScopesResources>("console-scopes-resources", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleCode>("console-code", explicitStart: true);
    RegisterClientIfEnabled<Projects.ConsoleResourceIndicators>("console-resource-indicators", explicitStart: true);
}

bool ClientIsEnabled(string name) => builder.Configuration
    .GetSection($"AspireProjectConfiguration:UseClients:{name}").Value?
    .Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;


void RegisterClientIfEnabled<T>(string name, bool explicitStart = false) where T : IProjectMetadata, new()
{
    if (ClientIsEnabled(typeof(T).Name))
    {
        var resourceBuilder = builder.AddProject<T>(name)
            .AddIdentityAndApiReferences(projectRegistry);
        if (explicitStart)
        {
            resourceBuilder.WithExplicitStart();
        }
    }
}
