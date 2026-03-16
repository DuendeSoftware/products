// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using IdentityServerHost;
namespace IdentityServer;

internal static class Extensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        _ = builder.Services.AddRazorPages();

        var isBuilder = builder.Services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                options.EmitStaticAudienceClaim = true;
            })
            .AddTestUsers(TestUsers.Users);

        // in-memory, code config
        _ = isBuilder.AddInMemoryIdentityResources(Config.IdentityResources);
        _ = isBuilder.AddInMemoryApiScopes(Config.ApiScopes);
        _ = isBuilder.AddInMemoryClients(Config.Clients);
        _ = isBuilder.AddInMemoryApiResources(Config.ApiResources);
        _ = isBuilder.AddExtensionGrantValidator<TokenExchangeGrantValidator>();

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        _ = app.UseHttpLogging();
        _ = app.UseDeveloperExceptionPage();
        _ = app.UseStaticFiles();

        _ = app.UseRouting();
        _ = app.UseIdentityServer();
        _ = app.UseAuthorization();
        _ = app.MapRazorPages()
            .RequireAuthorization();

        return app;
    }
}
