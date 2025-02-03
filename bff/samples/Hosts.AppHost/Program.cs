using Hosts.ServiceDefaults;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var idServer = builder.AddProject<Projects.IdentityServer>(AppHostServices.IdentityServer);

var api = builder.AddProject<Projects.Api>(AppHostServices.Api);
var isolatedApi = builder.AddProject<Projects.Api_Isolated>(AppHostServices.IsolatedApi);

var bff = builder.AddProject<Projects.Bff>(AppHostServices.Bff)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api)
    ;

var bffEf = builder.AddProject<Projects.Bff_EF>(AppHostServices.BffEf)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);

var bffBlazorWebAssembly = builder.AddProject<Projects.WebAssembly>(AppHostServices.BffBlazorWebassembly)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);


var bffBlazorPerComponent = builder.AddProject<Projects.PerComponent>(AppHostServices.BffBlazorPerComponent)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);

var apiDPop = builder.AddProject<Projects.Api_DPoP>(AppHostServices.ApiDpop);

var bffDPop = builder.AddProject<Projects.Bff_DPoP>(AppHostServices.BffDpop)
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