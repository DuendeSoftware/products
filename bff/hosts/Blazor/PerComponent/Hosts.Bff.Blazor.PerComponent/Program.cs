// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Blazor;
using Duende.Bff.Yarp;
using Hosts.Bff.Blazor.PerComponent;
using Hosts.Bff.Blazor.PerComponent.Client;
using Hosts.Bff.Blazor.PerComponent.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// BFF setup for blazor
_ = builder.Services.AddBff()
    .AddServerSideSessions()
    .AddBlazorServer()
    .AddRemoteApis();

_ = builder.Services.AddUserAccessTokenHttpClient("callApi",
    configureClient: client => client.BaseAddress = new Uri("https://localhost:5010/"));



// General blazor services
_ = builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
_ = builder.Services.AddCascadingAuthenticationState();

// Service used by the sample to describe where code is running
_ = builder.Services.AddScoped<IRenderModeContext, ServerRenderModeContext>();

_ = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "cookie";
        options.DefaultChallengeScheme = "oidc";
        options.DefaultSignOutScheme = "oidc";
    })
    .AddCookie("cookie", options =>
    {
        options.Cookie.Name = "__Host-blazor";
        options.Cookie.SameSite = SameSiteMode.Strict;
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = "https://localhost:5001";

        options.ClientId = "blazor";
        options.ClientSecret = "secret";
        options.ResponseType = "code";
        options.ResponseMode = "query";

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("api");
        options.Scope.Add("offline_access");

        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";

        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;

        options.SignOutScheme = "cookie";
    });

var app = builder.Build();

_ = app.UseHttpLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    _ = app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    _ = app.UseHsts();
}

_ = app.UseHttpsRedirection();

_ = app.UseStaticFiles();

_ = app.UseRouting();
_ = app.UseAuthentication();
_ = app.UseBff();
_ = app.UseAuthorization();
_ = app.UseAntiforgery();

_ = app.MapRemoteBffApiEndpoint("/remote-apis/user-token", new Uri("https://localhost:5010"))
    .WithAccessToken(RequiredTokenType.User);

_ = app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Hosts.Bff.Blazor.PerComponent.Client._Imports).Assembly);

app.Run();
