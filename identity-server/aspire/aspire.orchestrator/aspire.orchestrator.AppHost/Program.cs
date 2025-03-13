// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

var builder = DistributedApplication.CreateBuilder(args);

#region Identity Server Hosts

// These hosts don't require additional infrastructure
if (HostIsEnabled(nameof(Projects.Host_Main)))
    builder
        .AddProject<Projects.Host_Main>("host-main")
        .WithHttpsHealthCheck(path: "/.well-known/openid-configuration");

if (HostIsEnabled(nameof(Projects.Host_Configuration)))
    builder
        .AddProject<Projects.Host_Configuration>("host-configuration")
        .WithHttpsHealthCheck(path: "/.well-known/openid-configuration");

// These hosts require a database
if (HostIsEnabled(nameof(Projects.Host_AspNetIdentity)) || HostIsEnabled(nameof(Projects.Host_EntityFramework)))
{
    // Adds SQL Server to the builder (requires Docker).
    // Feel free to use your preferred docker management service.

    var sqlServer = builder
        .AddSqlServer(name: "SqlServer", port: 62949);

    var identityServerDb = sqlServer
        .AddDatabase(name: "IdentityServerDb", databaseName: "IdentityServer");

    if (HostIsEnabled(nameof(Projects.Host_AspNetIdentity)))
    {
        var aspnetMigration = builder.AddProject<Projects.AspNetIdentityDb>(name: "aspnetidentitydb-migrations")
            .WithReference(identityServerDb, connectionName: "DefaultConnection")
            .WaitFor(identityServerDb);

        builder.AddProject<Projects.Host_AspNetIdentity>(name: "host-aspnetidentity")
            .WithHttpsHealthCheck(path: "/.well-known/openid-configuration")
            .WithReference(identityServerDb, connectionName: "DefaultConnection")
            .WaitForCompletion(aspnetMigration);
    }

    if (HostIsEnabled(nameof(Projects.Host_EntityFramework)))
    {
        var idSrvMigration = builder.AddProject<Projects.IdentityServerDb>(name: "identityserverdb-migrations")
            .WithReference(identityServerDb, connectionName: "DefaultConnection")
            .WaitFor(identityServerDb);

        builder.AddProject<Projects.Host_EntityFramework>(name: "host-entityframework")
            .WithHttpsHealthCheck(path: "/.well-known/openid-configuration")
            .WithReference(identityServerDb, connectionName: "DefaultConnection")
            .WaitForCompletion(idSrvMigration);
    }
}

#endregion

#region Identity Server Clients

// ** Console-Code client ommitted as it currently requires interaction **
// ** Console-ResourceIndicators client ommitted as it current requires interaction **
// ** Console-ScopesResources client ommitted as it currently requires interaction **

if (ClientIsEnabled(nameof(Projects.MvcCode)))
    builder.AddProject<Projects.MvcCode>(name: "mvc-code");

if (ClientIsEnabled(nameof(Projects.MvcDPoP)))
    builder.AddProject<Projects.MvcDPoP>(name: "mvc-dpop");

if (ClientIsEnabled(nameof(Projects.JsOidc)))
    builder.AddProject<Projects.JsOidc>(name: "js-oidc");

if (ClientIsEnabled(nameof(Projects.MvcAutomaticTokenManagement)))
    builder.AddProject<Projects.MvcAutomaticTokenManagement>(name: "mvc-automatic-token-management");

if (ClientIsEnabled(nameof(Projects.MvcHybridBackChannel)))
    builder.AddProject<Projects.MvcHybridBackChannel>(name: "mvc-hybrid-backchannel");

if (ClientIsEnabled(nameof(Projects.MvcJarJwt)))
    builder.AddProject<Projects.MvcJarJwt>(name: "mvc-jar-jwt");

if (ClientIsEnabled(nameof(Projects.MvcJarUriJwt)))
    builder.AddProject<Projects.MvcJarUriJwt>(name: "mvc-jar-uri-jwt");

// These clients require a manual start
if (ClientIsEnabled(nameof(Projects.ConsoleCibaClient)))
    builder.AddProject<Projects.ConsoleCibaClient>(name: "console-ciba-client").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleDeviceFlow)))
    builder.AddProject<Projects.ConsoleDeviceFlow>(name: "console-device-flow").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleClientCredentialsFlow)))
    builder.AddProject<Projects.ConsoleClientCredentialsFlow>(name: "console-client-credentials-flow").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleClientCredentialsFlowCallingIdentityServerApi)))
    builder.AddProject<Projects.ConsoleClientCredentialsFlowCallingIdentityServerApi>(name: "console-client-credentials-flow-callingidentityserverapi").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleClientCredentialsFlowPostBody)))
    builder.AddProject<Projects.ConsoleClientCredentialsFlowPostBody>("console-client-credentials-flow-postbody").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleClientCredentialsFlowDPoP)))
    builder.AddProject<Projects.ConsoleClientCredentialsFlowDPoP>("console-client-credentials-flow-dpop").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleDcrClient)))
    builder.AddProject<Projects.ConsoleDcrClient>("console-dcr-client").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleEphemeralMtlsClient)))
    builder.AddProject<Projects.ConsoleEphemeralMtlsClient>("console-ephemeral-mtls-client").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleExtensionGrant)))
    builder.AddProject<Projects.ConsoleExtensionGrant>("console-extension-grant").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleIntrospectionClient)))
    builder.AddProject<Projects.ConsoleIntrospectionClient>("console-introspection-client").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleMTLSClient)))
    builder.AddProject<Projects.ConsoleMTLSClient>("console-mtls-client").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsolePrivateKeyJwtClient)))
    builder.AddProject<Projects.ConsolePrivateKeyJwtClient>("console-private-key-jwt-client").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleResourceOwnerFlow)))
    builder.AddProject<Projects.ConsoleResourceOwnerFlow>("console-resource-owner-flow").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleResourceOwnerFlowPublic)))
    builder.AddProject<Projects.ConsoleResourceOwnerFlowPublic>("console-resource-owner-flow-public").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleResourceOwnerFlowReference)))
    builder.AddProject<Projects.ConsoleResourceOwnerFlowReference>("console-resource-owner-flow-reference").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleResourceOwnerFlowRefreshToken)))
    builder.AddProject<Projects.ConsoleResourceOwnerFlowRefreshToken>("console-resource-owner-flow-refresh-token").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.ConsoleResourceOwnerFlowUserInfo)))
    builder.AddProject<Projects.ConsoleResourceOwnerFlowUserInfo>("console-resource-owner-flow-userinfo").WithExplicitStart();

if (ClientIsEnabled(nameof(Projects.WindowsConsoleSystemBrowser)))
    builder.AddProject<Projects.WindowsConsoleSystemBrowser>(name: "console-system-browser").WithExplicitStart();

#endregion

#region API Projects

if (ApiIsEnabled(nameof(Projects.SimpleApi)))
    builder.AddProject<Projects.SimpleApi>(name: "simple-api");

if (ApiIsEnabled(nameof(Projects.ResourceBasedApi)))
    builder.AddProject<Projects.ResourceBasedApi>("resource-based-api");

if (ApiIsEnabled(nameof(Projects.DPoPApi)))
    builder.AddProject<Projects.DPoPApi>(name: "dpop-api");

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
