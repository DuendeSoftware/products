// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Builder;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;

namespace Bff.Benchmarks.Hosts;

public class BffHost : Host
{
    public event Action<IBffServicesBuilder> OnConfigureBff = _ => { };

    internal BffHost(Uri bffUri, Uri identityServer, Uri apiUri, SimulatedInternet simulatedInternet) : base(bffUri, simulatedInternet)
    {
        OnConfigureServices += services =>
        {
            var bff = services
                .AddBff(opt =>
                {
                    if (!Internet.UseKestrel)
                    {
                        opt.BackchannelHttpHandler = simulatedInternet;
                    }
                })
                .ConfigureOpenIdConnect(oidc =>
                {
                    oidc.ClientId = "bff";
                    oidc.ClientSecret = "secret";
                    oidc.Authority = identityServer.ToString();
                    oidc.SaveTokens = true;
                    oidc.GetClaimsFromUserInfoEndpoint = true;
                    oidc.ResponseType = "code";
                    oidc.ResponseMode = "query";

                    oidc.MapInboundClaims = false;
                    oidc.GetClaimsFromUserInfoEndpoint = true;
                    oidc.SaveTokens = true;

                    // request scopes + refresh tokens
                    oidc.Scope.Clear();
                    oidc.Scope.Add("openid");
                    oidc.Scope.Add("profile");
                    oidc.Scope.Add("api");

                })
                .AddRemoteApis();
            if (!Internet.UseKestrel)
            {
                services.AddSingleton<IForwarderHttpClientFactory>(new SimulatedInternetYarpForwarderFactory(Internet));
            }
            OnConfigureBff(bff);
        };
        OnConfigure += app =>
        {
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseBff();
            app.MapGet("/", () => "bff");
            app.MapGet("/anon", () => "bff")
                .AllowAnonymous();

            app.MapRemoteBffApiEndpoint("/allow_anon", apiUri);
            app.MapRemoteBffApiEndpoint("/client_token", apiUri)
                .WithAccessToken(RequiredTokenType.Client);

            app.MapRemoteBffApiEndpoint("/user_token", apiUri)
                .WithAccessToken(RequiredTokenType.User);
        };
    }

    public void AddFrontend(BffFrontendName name) =>
        GetService<IFrontendCollection>()
            .AddOrUpdate(new BffFrontend(name));


    public void AddFrontend(Uri uri) =>
        GetService<IFrontendCollection>()
        .AddOrUpdate(new BffFrontend(BffFrontendName.Parse(uri.Host + "-" + uri.Port))
            .MappedToOrigin(Origin.Parse(uri)));

    public void AddFrontend(LocalPath path) =>
        GetService<IFrontendCollection>()
            .AddOrUpdate(new BffFrontend(BffFrontendName.Parse(path.ToString().Replace("/", "")))
                .MappedToPath(path));
}

internal class SimulatedInternetYarpForwarderFactory(SimulatedInternet simulatedInternet)
    : IForwarderHttpClientFactory
{
    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => new HttpMessageInvoker(simulatedInternet);
}
