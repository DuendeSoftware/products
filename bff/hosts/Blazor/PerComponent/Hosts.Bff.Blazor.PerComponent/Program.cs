// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.Bff.Blazor;
using Duende.Bff.Yarp;
using Hosts.Bff.Blazor.PerComponent;
using Hosts.Bff.Blazor.PerComponent.Client;
using Hosts.Bff.Blazor.PerComponent.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// BFF setup for blazor
builder.Services.AddBff()
    .AddServerSideSessions()
    .AddBlazorServer()
    .AddRemoteApis();

builder.Services.AddUserAccessTokenHttpClient("callApi",
    configureClient: client => client.BaseAddress = new Uri("https://localhost:5010/"));



// General blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddCascadingAuthenticationState();

// Service used by the sample to describe where code is running
builder.Services.AddScoped<IRenderModeContext, ServerRenderModeContext>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "cookie";
        options.DefaultChallengeScheme = "oidc";
        options.DefaultSignOutScheme = "oidc";
    })
    .AddCookie("cookie", options =>
    {
        options.Cookie.Name = "__Host-blazor";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Events.OnValidatePrincipal += context =>
        {
            return Task.CompletedTask;
        };
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

app.UseHttpLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseBff();
app.UseAuthorization();
app.UseAntiforgery();

app.MapBffManagementEndpoints();

app.MapRemoteBffApiEndpoint("/remote-apis/user-token", "https://localhost:5010")
    .RequireAccessToken(TokenType.User);

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Hosts.Bff.Blazor.PerComponent.Client._Imports).Assembly);

app.Run();
