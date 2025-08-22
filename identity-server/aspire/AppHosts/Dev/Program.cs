// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

var builder = DistributedApplication.CreateBuilder(args);

var identityServerHost = builder.AddProject<Projects.Host_EntityFramework_dotnet9>(name: "is-host")
    .WithHttpHealthCheck(path: "/.well-known/openid-configuration");

var api = builder.AddProject<Projects.DPoPApi>("dpop-api")
    .WithEnvironment(
        name: "is-host",
        endpointReference: identityServerHost.GetEndpoint(name: "https")
    );

var webClient = builder.AddProject<Projects.Web>("web")
    .WithReference(identityServerHost)
    .WithEnvironment(name: "is-host", endpointReference: identityServerHost.GetEndpoint(name: "https"))
    .WithReference(api)
    .WithEnvironment(name: "dpop-api", endpointReference: api.GetEndpoint(name: "https"));

builder.Build().Run();
