// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Bff;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.Yarp;
using Hosts.ServiceDefaults;

Console.Title = "BFF";

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var services = builder.Services;

// Add BFF services to DI - also add server-side session management
services.AddBff()
    .WithDefaultOpenIdConnectOptions(options =>
    {
        var authority = ServiceDiscovery.ResolveService(AppHostServices.IdentityServer);
        options.Authority = authority.ToString();

        // confidential client using code flow + PKCE
        options.ClientId = "bff";
        options.ClientSecret = "secret";
        options.ResponseType = "code";
        options.ResponseMode = "query";

        options.MapInboundClaims = false;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;

        // request scopes + refresh tokens
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("api");
        options.Scope.Add("scope-for-isolated-api");
        options.Scope.Add("offline_access");

        options.Resource = "urn:isolated-api";
    })
    .AddRemoteApis()
    .AddServerSideSessions();

// local APIs
services.AddControllers();

services.AddTransient<ImpersonationAccessTokenRetriever>();

services.AddUserAccessTokenHttpClient("api",
    configureClient: client => { client.BaseAddress = new Uri("https://localhost:5010/api"); });


var app = builder.Build();
app.UseHttpLogging();
app.UseDeveloperExceptionPage();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseRouting();

// adds antiforgery protection for local APIs
app.UseBff();

// adds authorization for local and remote API endpoints
app.UseAuthorization();

// local APIs
app.MapControllers()
    .RequireAuthorization()
    .AsBffApiEndpoint();

//////////////////////////////////////
// proxy app for cross-site APIs
//////////////////////////////////////

// On this path, we use a client credentials token
app.MapRemoteBffApiEndpoint("/api/client-token", new Uri("https://localhost:5010"))
    .WithAccessToken(RequiredTokenType.Client);

// On this path, we use a user token if logged in, and fall back to a client credentials token if not
app.MapRemoteBffApiEndpoint("/api/user-or-client-token", new Uri("https://localhost:5010"))
    .WithAccessToken(RequiredTokenType.UserOrClient);

// On this path, we make anonymous requests
app.MapRemoteBffApiEndpoint("/api/anonymous", new Uri("https://localhost:5010"));

// On this path, we use the client token only if the user is logged in
app.MapRemoteBffApiEndpoint("/api/optional-user-token", new Uri("https://localhost:5010"))
    .WithAccessToken(RequiredTokenType.UserOrNone);

// On this path, we require the user token
app.MapRemoteBffApiEndpoint("/api/user-token", new Uri("https://localhost:5010"))
    .WithAccessToken();

// On this path, we perform token exchange to impersonate a different user
// before making the api request
app.MapRemoteBffApiEndpoint("/api/impersonation", new Uri("https://localhost:5010"))
    .WithAccessToken()
    .WithAccessTokenRetriever<ImpersonationAccessTokenRetriever>();

// On this path, we obtain an audience constrained token and invoke
// a different api that requires such a token
app.MapRemoteBffApiEndpoint("/api/audience-constrained", new Uri("https://localhost:5012"))
    .WithAccessToken()
    .WithUserAccessTokenParameter(new BffUserAccessTokenParameters { Resource = Resource.Parse("urn:isolated-api") });

app.Run();
