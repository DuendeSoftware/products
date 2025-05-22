// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using aspire.orchestrator.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Create a collection of the project resources to be used by the orchestrator
// This will allow us to refer back to the projects when setting up references.
var projectRegistry = new Dictionary<string, IResourceBuilder<ProjectResource>>();

#region Identity Server Hosts

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

#endregion

#region API Projects

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

#endregion

#region Identity Server Clients

if (ClientIsEnabled(nameof(Projects.MvcCode)))
{
    builder.AddProject<Projects.MvcCode>(name: "mvc-code")
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.MvcDPoP)))
{
    builder.AddProject<Projects.MvcDPoP>(name: "mvc-dpop")
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.JsOidc)))
{
    builder.AddProject<Projects.JsOidc>(name: "js-oidc")
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.MvcAutomaticTokenManagement)))
{
    builder.AddProject<Projects.MvcAutomaticTokenManagement>(name: "mvc-automatic-token-management")
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.MvcHybridBackChannel)))
{
    builder.AddProject<Projects.MvcHybridBackChannel>(name: "mvc-hybrid-backchannel")
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.MvcJarJwt)))
{
    builder.AddProject<Projects.MvcJarJwt>(name: "mvc-jar-jwt")
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.MvcJarUriJwt)))
{
    builder.AddProject<Projects.MvcJarUriJwt>(name: "mvc-jar-uri-jwt")
        .AddIdentityAndApiReferences(projectRegistry);
}

// These clients require a manual start
if (ClientIsEnabled(nameof(Projects.ConsoleCibaClient)))
{
    builder.AddProject<Projects.ConsoleCibaClient>(name: "console-ciba-client")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleDeviceFlow)))
{
    builder.AddProject<Projects.ConsoleDeviceFlow>(name: "console-device-flow")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleClientCredentialsFlow)))
{
    builder.AddProject<Projects.ConsoleClientCredentialsFlow>(name: "console-client-credentials-flow")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleClientCredentialsFlowCallingIdentityServerApi)))
{
    builder.AddProject<Projects.ConsoleClientCredentialsFlowCallingIdentityServerApi>(name: "console-client-credentials-flow-callingidentityserverapi")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleClientCredentialsFlowPostBody)))
{
    builder.AddProject<Projects.ConsoleClientCredentialsFlowPostBody>(name: "console-client-credentials-flow-postbody")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleClientCredentialsFlowDPoP)))
{
    builder.AddProject<Projects.ConsoleClientCredentialsFlowDPoP>(name: "console-client-credentials-flow-dpop")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleDcrClient)))
{
    builder.AddProject<Projects.ConsoleDcrClient>(name: "console-dcr-client")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleEphemeralMtlsClient)))
{
    builder.AddProject<Projects.ConsoleEphemeralMtlsClient>(name: "console-ephemeral-mtls-client")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleExtensionGrant)))
{
    builder.AddProject<Projects.ConsoleExtensionGrant>(name: "console-extension-grant")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleIntrospectionClient)))
{
    builder.AddProject<Projects.ConsoleIntrospectionClient>(name: "console-introspection-client")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleMTLSClient)))
{
    builder.AddProject<Projects.ConsoleMTLSClient>(name: "console-mtls-client")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsolePrivateKeyJwtClient)))
{
    builder.AddProject<Projects.ConsolePrivateKeyJwtClient>(name: "console-private-key-jwt-client")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleResourceOwnerFlow)))
{
    builder.AddProject<Projects.ConsoleResourceOwnerFlow>(name: "console-resource-owner-flow")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleResourceOwnerFlowPublic)))
{
    builder.AddProject<Projects.ConsoleResourceOwnerFlowPublic>(name: "console-resource-owner-flow-public")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleResourceOwnerFlowReference)))
{
    builder.AddProject<Projects.ConsoleResourceOwnerFlowReference>(name: "console-resource-owner-flow-reference")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleResourceOwnerFlowRefreshToken)))
{
    builder.AddProject<Projects.ConsoleResourceOwnerFlowRefreshToken>(name: "console-resource-owner-flow-refresh-token")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleResourceOwnerFlowUserInfo)))
{
    builder.AddProject<Projects.ConsoleResourceOwnerFlowUserInfo>(name: "console-resource-owner-flow-userinfo")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.WindowsConsoleSystemBrowser)))
{
    builder.AddProject<Projects.WindowsConsoleSystemBrowser>(name: "console-system-browser")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleScopesResources)))
{
    builder.AddProject<Projects.ConsoleScopesResources>(name: "console-scopes-resources")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleCode)))
{
    builder.AddProject<Projects.ConsoleCode>(name: "console-code")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

if (ClientIsEnabled(nameof(Projects.ConsoleResourceIndicators)))
{
    builder.AddProject<Projects.ConsoleResourceIndicators>(name: "console-resource-indicators")
        .WithExplicitStart()
        .AddIdentityAndApiReferences(projectRegistry);
}

#endregion

builder.Build().Run();

bool HostIsEnabled(string name) => builder.Configuration
    .GetSection($"AspireProjectConfiguration:IdentityHost").Value?
    .Equals(name, StringComparison.OrdinalIgnoreCase) ?? false;

bool ClientIsEnabled(string name) => builder.Configuration
    .GetSection($"AspireProjectConfiguration:UseClients:{name}").Value?
    .Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

bool ApiIsEnabled(string name) => builder.Configuration
    .GetSection($"AspireProjectConfiguration:UseApis:{name}").Value?
    .Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
