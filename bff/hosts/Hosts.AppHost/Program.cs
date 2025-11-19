// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Hosts.ServiceDefaults;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var idServer = builder.AddProject<Projects.Hosts_IdentityServer>(AppHostServices.IdentityServer);

var api = builder.AddProject<Projects.Hosts_RemoteApi>(AppHostServices.Api);
var isolatedApi = builder.AddProject<Projects.Hosts_RemoteApi_Isolated>(AppHostServices.IsolatedApi);

var bff = builder.AddProject<Projects.Hosts_Bff_InMemory>(AppHostServices.Bff)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api)
    ;

var perf = builder.AddProject<Projects.Hosts_Bff_Performance>(AppHostServices.BffPerf)
   .WithExternalHttpEndpoints()
   .WithEndpoint(6100, isProxied: false, scheme: "https", name: "idsrv")
   .WithEndpoint(6001, isProxied: false, scheme: "https", name: "api")
   .WithEndpoint(6002, isProxied: false, scheme: "https", name: "single")
   .WithEndpoint(6003, isProxied: false, scheme: "https", name: "multi")
   .WithEndpoint(6004, isProxied: false, scheme: "https", name: "multi2")
   ;

var bffMulti = builder.AddProject<Projects.Hosts_Bff_MultiFrontend>(AppHostServices.BffMultiFrontend)
    .WithExternalHttpEndpoints()
    .WithUrl("https://app1.localhost:5005", "https://app1.localhost:5005")
    .WithUrl("https://localhost:5005/with-path/", "https://localhost/with-path/")
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api)
    ;


var bffEf = builder.AddProject<Projects.Hosts_Bff_EF>(AppHostServices.BffEf)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);

var bffBlazorWebAssembly = builder.AddProject<Projects.Hosts_Bff_Blazor_WebAssembly>(AppHostServices.BffBlazorWebassembly)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);


var bffBlazorPerComponent = builder.AddProject<Projects.Hosts_Bff_Blazor_PerComponent>(AppHostServices.BffBlazorPerComponent)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);

var apiDPop = builder.AddProject<Projects.Hosts_RemoteApi_DPoP>(AppHostServices.ApiDpop);

var bffDPop = builder.AddProject<Projects.Hosts_Bff_DPoP>(AppHostServices.BffDpop)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(apiDPop);

builder.AddProject<Projects.UserSessionDb>(AppHostServices.Migrations);

idServer
    .WithReference(bff)
    .WithReference(perf)
    .WithReference(bffMulti)
    .WithReference(bffEf)
    .WithReference(bffBlazorPerComponent)
    .WithReference(bffBlazorWebAssembly)
    .WithReference(apiDPop)
    .WithReference(bffDPop)
    ;

builder.AddProject<BffLocalApi>(AppHostServices.TemplateBffLocal, launchProfileName: null)
    .WithHttpsEndpoint(5300, name: "bff-local");

builder.AddProject<BffRemoteApi>(AppHostServices.TemplateBffRemote, launchProfileName: null)
    .WithHttpsEndpoint(5310, name: "bff-remote");

builder.AddProject<BffBlazorAutoRenderMode>(AppHostServices.TemplateBffBlazor);

builder.Build().Run();

public static class Extensions
{
    public static IResourceBuilder<TDestination> WithAwaitedReference<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<IResourceWithServiceDiscovery> source)
        where TDestination : IResourceWithEnvironment, IResourceWithWaitSupport => builder.WithReference(source).WaitFor(source);
}
