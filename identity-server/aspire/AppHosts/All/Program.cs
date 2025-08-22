// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using AppHosts.All;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Create a collection of the project resources to be used by the orchestrator
// This will allow us to refer back to the projects when setting up references.
var projectRegistry = new Dictionary<string, IResourceBuilder<ProjectResource>>();

// Configure options
var appConfig = builder.Configuration
    .GetSection("AspireProjectConfiguration")
    .Get<AppHostConfiguration>();
if (appConfig is null)
{
    throw new InvalidOperationException("AspireProjectConfiguration not found in appsettings.json");
}

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
            .WithHttpHealthCheck(path: "/.well-known/openid-configuration");

        projectRegistry.Add("is-host", hostMain);
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
            var hostAspNetIdentity = builder.AddProject<Projects.Host_AspNetIdentity>(name: "is-host")
                .WithHttpHealthCheck(path: "/.well-known/openid-configuration")
                .WithReference(identityServerDb, connectionName: "DefaultConnection");

            if (appConfig.RunDatabaseMigrations)
            {
                var aspnetMigration = builder.AddProject<Projects.AspNetIdentityDb>(name: "aspnetidentitydb-migrations")
                    .WithReference(identityServerDb, connectionName: "DefaultConnection")
                    .WaitFor(identityServerDb);
                hostAspNetIdentity.WaitForCompletion(aspnetMigration);
            }

            projectRegistry.Add("is-host", hostAspNetIdentity);
        }

        if (HostIsEnabled(nameof(Projects.Host_EntityFramework)))
        {
            var hostEntityFramework = builder.AddProject<Projects.Host_EntityFramework>(name: "is-host")
                .WithHttpHealthCheck(path: "/.well-known/openid-configuration")
                .WithReference(identityServerDb, connectionName: "DefaultConnection");

            if (appConfig.RunDatabaseMigrations)
            {
                var idSrvMigration = builder.AddProject<Projects.IdentityServerDb>(name: "identityserverdb-migrations")
                    .WithReference(identityServerDb, connectionName: "DefaultConnection")
                    .WaitFor(identityServerDb);
                hostEntityFramework.WaitForCompletion(idSrvMigration);
            }

            projectRegistry.Add("is-host", hostEntityFramework);
        }

        if (HostIsEnabled(nameof(Projects.Host_EntityFramework_dotnet9)))
        {
            var hostEntityFramework = builder.AddProject<Projects.Host_EntityFramework_dotnet9>(name: "is-host")
                .WithHttpHealthCheck(path: "/.well-known/openid-configuration")
                .WithReference(identityServerDb, connectionName: "DefaultConnection");

            if (appConfig.RunDatabaseMigrations)
            {
                var idSrvMigration = builder.AddProject<Projects.IdentityServerDb>(name: "identityserverdb-migrations")
                    .WithReference(identityServerDb, connectionName: "DefaultConnection")
                    .WaitFor(identityServerDb);
                hostEntityFramework.WaitForCompletion(idSrvMigration);
            }

            projectRegistry.Add("is-host", hostEntityFramework);
        }
    }

    bool HostIsEnabled(string name) =>
        appConfig.IdentityHost?.Equals(name, StringComparison.OrdinalIgnoreCase) ?? false;
}

void ConfigureApis()
{
    RegisterApiIfEnabled<Projects.SimpleApi>("simple-api");
    RegisterApiIfEnabled<Projects.ResourceBasedApi>("resource-based-api");
    RegisterApiIfEnabled<Projects.DPoPApi>("dpop-api");
    RegisterApiIfEnabled<Projects.MtlsApi>("mtls-api");
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
    RegisterClientIfEnabled<Projects.Web>("web");
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
    RegisterClientIfEnabled<Projects.ConsoleParameterizedScopeClient>("console-parameterized-scope-client", explicitStart: true);
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

bool ClientIsEnabled(string name)
{
    if (appConfig.UseClients == null)
    {
        return false;
    }

    return appConfig.UseClients.TryGetValue(name, out var enabled) && enabled;
}

void RegisterApiIfEnabled<T>(string name) where T : IProjectMetadata, new()
{
    if (ApiIsEnabled(typeof(T).Name))
    {
        var api = builder.AddProject<T>(name)
            .WithEnvironment(
                name: "is-host",
                endpointReference: projectRegistry["is-host"].GetEndpoint(name: "https")
            );
        projectRegistry.Add(name, api);
    }
}

bool ApiIsEnabled(string name)
{
    if (appConfig.UseApis == null)
    {
        return false;
    }

    return appConfig.UseApis.TryGetValue(name, out var enabled) && enabled;
}

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
