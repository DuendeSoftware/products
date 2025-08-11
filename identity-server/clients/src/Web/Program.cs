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
builder.AddServiceDefaults();
var authority = builder.Configuration["is-host"];

// Add services to the container.
builder.Services.AddRazorPages();
AddAuthentication();
AddAccessTokenManagement();
ConfigureBackChannelLogout();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

void AddAccessTokenManagement()
{
    builder.Services.AddOpenIdConnectAccessTokenManagement(options =>
    {
        // add option to opt-in to jkt on authZ ep
        // create and configure a DPoP JWK
        var rsaKey = new RsaSecurityKey(RSA.Create(2048));
        var jwk = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
        jwk.Alg = "PS256";
        options.DPoPJsonWebKey = DPoPProofKey.Parse(JsonSerializer.Serialize(jwk));
    });
    builder.Services.AddUserAccessTokenHttpClient("client", configureClient: client =>
    {
        client.BaseAddress = new Uri("https://dpop-api");
    }).AddServiceDiscovery();

    builder.Services.AddTransient<IClientAssertionService, ClientAssertionService>();
    builder.Services.AddSingleton<AssertionService>();
}

void AddAuthentication()
{
    builder.Services.AddAuthentication(options =>
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
    builder.Services.ConfigureOptions<ConfigureAssertionsAndJar>();
}

void ConfigureBackChannelLogout()
{
    builder.Services.AddTransient<LogoutEvents>();
    builder.Services.AddSingleton<LogoutSessionManager>();
}
