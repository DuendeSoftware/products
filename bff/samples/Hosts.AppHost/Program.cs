var builder = DistributedApplication.CreateBuilder(args);

var idServer = builder.AddProject<Projects.IdentityServer>("identity-server");

var api = builder.AddProject<Projects.Api>("api");
var isolatedApi = builder.AddProject<Projects.Api_Isolated>("api-isolated");

var bff = builder.AddProject<Projects.Bff>("bff")
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api)
    ;

builder.AddProject<Projects.Bff_EF>("bff-ef")
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);

builder.AddProject<Projects.WebAssembly>("bff-blazor-webassembly")
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);


builder.AddProject<Projects.PerComponent>("bff-blazor-server")
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(isolatedApi)
    .WithAwaitedReference(api);

var apiDPop = builder.AddProject<Projects.Api_DPoP>("api-dpop");

builder.AddProject<Projects.Bff_DPoP>("bff-dpop")
    .WithExternalHttpEndpoints()
    .WithAwaitedReference(idServer)
    .WithAwaitedReference(apiDPop);

builder.AddProject<Projects.UserSessionDb>("migrations");

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