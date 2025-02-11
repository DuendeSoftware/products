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
    .WithReference(bffEf)
    .WithReference(bffBlazorPerComponent)
    .WithReference(bffBlazorWebAssembly)
    .WithReference(apiDPop)
    .WithReference(bffDPop)
    ;

builder.Build().Run();

public static class Extensions
{
    public static IResourceBuilder<TDestination> WithAwaitedReference<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<IResourceWithServiceDiscovery> source)
        where TDestination : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        return builder.WithReference(source).WaitFor(source);
    }
}