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
    if (HostIsEnabled(nameof(Projects.Host_Main10)))
    {
        var hostMain = builder
            .AddProject<Projects.Host_Main10>("is-host")
            .WithHttpHealthCheck(path: "/.well-known/openid-configuration");

        projectRegistry.Add("is-host", hostMain);
    }


    // These hosts require a database
    var dbHosts = new List<string>
    {
        nameof(Projects.Host_AspNetIdentity10),
        nameof(Projects.Host_EntityFramework10)
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

        if (HostIsEnabled(nameof(Projects.Host_AspNetIdentity10)))
        {
            var hostAspNetIdentity = builder.AddProject<Projects.Host_AspNetIdentity10>(name: "is-host")
                .WithHttpHealthCheck(path: "/.well-known/openid-configuration")
                .WithReference(identityServerDb, connectionName: "DefaultConnection");

            if (appConfig.RunDatabaseMigrations)
            {
                var aspnetMigration = builder.AddProject<Projects.AspNetIdentityDb>(name: "aspnetidentitydb-migrations")
                    .WithReference(identityServerDb, connectionName: "DefaultConnection")
                    .WaitFor(identityServerDb);
                _ = hostAspNetIdentity.WaitForCompletion(aspnetMigration);
            }

            projectRegistry.Add("is-host", hostAspNetIdentity);
        }

        if (HostIsEnabled(nameof(Projects.Host_EntityFramework10)))
        {
            var hostEntityFramework = builder.AddProject<Projects.Host_EntityFramework10>(name: "is-host")
                .WithHttpHealthCheck(path: "/.well-known/openid-configuration")
                .WithReference(identityServerDb, connectionName: "DefaultConnection");

            if (appConfig.RunDatabaseMigrations)
            {
                var idSrvMigration = builder.AddProject<Projects.IdentityServerDb>(name: "identityserverdb-migrations")
                    .WithReference(identityServerDb, connectionName: "DefaultConnection")
                    .WaitFor(identityServerDb);
                _ = hostEntityFramework.WaitForCompletion(idSrvMigration);
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
    _ = RegisterClientIfEnabled<Projects.MvcCode>("mvc-code");
    _ = RegisterClientIfEnabled<Projects.MvcDPoP>("mvc-dpop");
    _ = RegisterClientIfEnabled<Projects.JsOidc>("js-oidc");
    _ = RegisterClientIfEnabled<Projects.MvcAutomaticTokenManagement>("mvc-automatic-token-management");
    _ = RegisterClientIfEnabled<Projects.MvcHybridBackChannel>("mvc-hybrid-backchannel");
    _ = RegisterClientIfEnabled<Projects.MvcJarJwt>("mvc-jar-jwt");
    _ = RegisterClientIfEnabled<Projects.MvcJarUriJwt>("mvc-jar-uri-jwt");
    _ = RegisterClientIfEnabled<Projects.MvcSaml>("mvc-saml");
    _ = RegisterClientIfEnabled<Projects.Web>("web");
    _ = RegisterTemplateIfEnabled<Projects.IdentityServerTemplate>("template-is", 7001);
    _ = RegisterTemplateIfEnabled<Projects.IdentityServerEmpty>("template-is-empty", 7002);
    _ = RegisterTemplateIfEnabled<Projects.IdentityServerInMem>("template-is-inmem", 7003);
    _ = RegisterTemplateIfEnabled<Projects.IdentityServerAspNetIdentity>("template-is-aspid", 7004);
    _ = RegisterTemplateIfEnabled<Projects.IdentityServerEntityFramework>("template-is-ef", 7005);
}

void ConfigureConsoleClients()
{
    _ = RegisterClientIfEnabled<Projects.ConsoleCibaClient>("console-ciba-client", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleDeviceFlow>("console-device-flow", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleClientCredentialsFlow>("console-client-credentials-flow", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleClientCredentialsFlowCallingIdentityServerApi>("console-client-credentials-flow-callingidentityserverapi", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleClientCredentialsFlowPostBody>("console-client-credentials-flow-postbody", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleClientCredentialsFlowDPoP>("console-client-credentials-flow-dpop", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleDcrClient>("console-dcr-client", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleEphemeralMtlsClient>("console-ephemeral-mtls-client", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleExtensionGrant>("console-extension-grant", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleIntrospectionClient>("console-introspection-client", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleMTLSClient>("console-mtls-client", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleParameterizedScopeClient>("console-parameterized-scope-client", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsolePrivateKeyJwtClient>("console-private-key-jwt-client", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleResourceOwnerFlow>("console-resource-owner-flow", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleResourceOwnerFlowPublic>("console-resource-owner-flow-public", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleResourceOwnerFlowReference>("console-resource-owner-flow-reference", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleResourceOwnerFlowRefreshToken>("console-resource-owner-flow-refresh-token", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleResourceOwnerFlowUserInfo>("console-resource-owner-flow-userinfo", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.WindowsConsoleSystemBrowser>("console-system-browser", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleScopesResources>("console-scopes-resources", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleCode>("console-code", explicitStart: true);
    _ = RegisterClientIfEnabled<Projects.ConsoleResourceIndicators>("console-resource-indicators", explicitStart: true);
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

IResourceBuilder<ProjectResource>? RegisterTemplateIfEnabled<T>(string name, int port) where T : IProjectMetadata, new() =>
    RegisterClientIfEnabled<T>(name, useLaunchProfile: false)?.WithHttpsEndpoint(port: port);

IResourceBuilder<ProjectResource>? RegisterClientIfEnabled<T>(string name, bool explicitStart = false, bool useLaunchProfile = true) where T : IProjectMetadata, new()
{
    if (ClientIsEnabled(typeof(T).Name))
    {
        var resourceBuilder = useLaunchProfile ?
            builder.AddProject<T>(name) :
            builder.AddProject<T>(name, launchProfileName: null);
        _ = resourceBuilder.AddIdentityAndApiReferences(projectRegistry);
        if (explicitStart)
        {
            _ = resourceBuilder.WithExplicitStart();
        }

        return resourceBuilder;
    }

    return null;
}
