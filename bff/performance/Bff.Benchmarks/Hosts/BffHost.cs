// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.Builder;

namespace Bff.Benchmarks.Hosts;

public class BffHost : Host
{
    public BffHost(Uri identityServer, Uri apiUri)
    {
        OnConfigureServices += services =>
        {
            services.AddBff()
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
        };
        OnConfigure += app =>
        {
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseBff();
            app.MapGet("/", () => "bff");

            app.MapRemoteBffApiEndpoint("/allow_anon", apiUri);
            app.MapRemoteBffApiEndpoint("/client_token", apiUri)
                .WithAccessToken(RequiredTokenType.Client);

            app.MapRemoteBffApiEndpoint("/user_token", apiUri)
                .WithAccessToken(RequiredTokenType.User);
        };
    }

    public void AddFrontend(Uri uri) => GetService<IFrontendCollection>().AddOrUpdate(new BffFrontend(BffFrontendName.Parse(uri.ToString())).MappedToOrigin(Origin.Parse(uri)));
}
