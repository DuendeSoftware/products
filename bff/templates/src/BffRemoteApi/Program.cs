using BffRemoteApi;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBff()
    .AddRemoteApis();

Configuration config = new();
builder.Configuration.Bind("BFF", config);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "cookie";
        options.DefaultChallengeScheme = "oidc";
        options.DefaultSignOutScheme = "oidc";
    })
    .AddCookie("cookie", options =>
    {
        options.Cookie.Name = "__Host-bff";
        options.Cookie.SameSite = SameSiteMode.Strict;
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = config.Authority;
        options.ClientId = config.ClientId;
        options.ClientSecret = config.ClientSecret;

        options.ResponseType = "code";
        options.ResponseMode = "query";

        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.SaveTokens = true;

        options.Scope.Clear();
        foreach (var scope in config.Scopes)
        {
            options.Scope.Add(scope);
        }

            options.TokenValidationParameters = new()
        {
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

// Add `.PersistKeysTo…()` and `.ProtectKeysWith…()`calls
// See more at https://docs.duendesoftware.com/general/data-protection
_ = builder.Services.AddDataProtection()
    .SetApplicationName("BFF");


var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseBff();

app.MapBffManagementEndpoints();

if (config.Apis.Any())
{
    foreach (var api in config.Apis)
    {
        _ = app.MapRemoteBffApiEndpoint(api.PathMatch, api.RemoteUrl!)
            .RequireAccessToken(api.RequiredToken);
    }
}

app.Run();
