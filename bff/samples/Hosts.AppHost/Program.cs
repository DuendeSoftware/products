using Hosts.ServiceDefaults;

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

builder.AddProject<Projects.Bff_EF>(AppHostServices.BffEf)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);

builder.AddProject<Projects.WebAssembly>(AppHostServices.BffBlazorWebassembly)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);


builder.AddProject<Projects.PerComponent>(AppHostServices.BffBlazorPerComponent)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);

var apiDPop = builder.AddProject<Projects.Api_DPoP>(AppHostServices.ApiDpop);

builder.AddProject<Projects.Bff_DPoP>(AppHostServices.BffDpop)
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(apiDPop);

builder.AddProject<Projects.UserSessionDb>(AppHostServices.Migrations);

idServer.WithReference(bff);

builder.Build().Run();

public static class Extensions
{
    public static IResourceBuilder<TDestination> WithAwaitedReference<TDestination>(this IResourceBuilder<TDestination> builder, IResourceBuilder<IResourceWithServiceDiscovery> source)
        where TDestination : IResourceWithEnvironment, IResourceWithWaitSupport
    {
        return builder.WithReference(source).WaitFor(source);
    }
}