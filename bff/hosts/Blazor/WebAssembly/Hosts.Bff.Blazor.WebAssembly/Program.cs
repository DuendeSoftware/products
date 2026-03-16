// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Hosts.Bff.Blazor.WebAssembly;
using Hosts.Bff.Blazor.WebAssembly.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

_ = builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

_ = builder.Services.AddBff();

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

        // confidential client using code flow + PKCE
        options.ClientId = "blazor";
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
        options.Scope.Add("offline_access");
    });
_ = builder.Services.AddCascadingAuthenticationState();
_ = builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

_ = builder.Services.AddAuthorization();

var app = builder.Build();

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

_ = app.UseHttpLogging();

_ = app.UseHttpsRedirection();

_ = app.UseStaticFiles();

_ = app.UseRouting();
_ = app.UseAuthentication();
_ = app.UseBff();
_ = app.UseAuthorization();
_ = app.UseAntiforgery();

_ = app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Hosts.Bff.Blazor.WebAssembly.Client._Imports).Assembly);

WeatherEndpoints.Map(app);

app.Run();
