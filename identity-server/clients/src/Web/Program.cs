// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Duende.AccessTokenManagement.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Web;

var builder = WebApplication.CreateBuilder(args);
_ = builder.AddServiceDefaults();
var authority = builder.Configuration["is-host"];

// Add services to the container.
_ = builder.Services.AddRazorPages();
AddAuthentication();
AddAccessTokenManagement();
ConfigureBackChannelLogout();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    _ = app.UseHsts();
}

_ = app.UseHttpsRedirection();

_ = app.UseRouting();

_ = app.UseAuthentication();
_ = app.UseAuthorization();

_ = app.MapStaticAssets();
_ = app.MapRazorPages()
   .WithStaticAssets();

app.Run();

void AddAccessTokenManagement()
{
    _ = builder.Services.AddOpenIdConnectAccessTokenManagement(options =>
    {
        // add option to opt-in to jkt on authZ ep
        // create and configure a DPoP JWK
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jwk = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
        jwk.Alg = "PS256";
        options.DPoPJsonWebKey = DPoPProofKey.Parse(JsonSerializer.Serialize(jwk));
    });
    _ = builder.Services.AddUserAccessTokenHttpClient("client", configureClient: client =>
    {
        client.BaseAddress = new Uri("https://dpop-api");
    }).AddServiceDiscovery();

    _ = builder.Services.AddTransient<IClientAssertionService, ClientAssertionService>();
    _ = builder.Services.AddSingleton<AssertionService>();
}

void AddAuthentication()
{
    _ = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "cookie";
        options.DefaultChallengeScheme = "oidc";
    }).AddCookie("cookie", options =>
    {
        options.Cookie.Name = "Web";
        options.EventsType = typeof(LogoutEvents);
    }).AddOpenIdConnect("oidc", options =>
    {
        options.Authority = authority;

        options.ClientId = "web";

        options.ResponseType = "code";
        options.ResponseMode = "query";

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("resource1.scope1");
        options.Scope.Add("offline_access");

        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        options.DisableTelemetry = true;
    });
    _ = builder.Services.ConfigureOptions<ConfigureAssertionsAndJar>();
}

void ConfigureBackChannelLogout()
{
    _ = builder.Services.AddTransient<LogoutEvents>();
    _ = builder.Services.AddSingleton<LogoutSessionManager>();
}
