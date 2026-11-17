// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using AuthenticationFlowWithWebComponent;
using AuthenticationFlowWithWebComponent.ServerComponent;
using AuthenticationFlowWithWebComponent.ServerComponent.ExternalAuthentication;
using Duende.IdentityServer;
using Duende.Storage;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.PostgreSql;
using Duende.Storage.Schema;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages()
    .AddRazorRuntimeCompilation();

builder.Services.AddSingleton(HtmlEncoder.Default);
builder.Services.AddSingleton(UrlEncoder.Default);

// Add and configure Persona
builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("pgsql")!).Build());

builder.Services
    .AddIdentityServer()
    .AddStorage(storage => storage.AddPostgreSql())
    .AddUserManagement(StorageInstanceId.Default, users =>
    {
        users.Authentication(authentication =>
                authentication.UseSmtpOtpDispatcher(options => builder.Configuration.GetSection("Smtp").Bind(options)));
    })
    // This sample creates its profile schema at startup through ISchemaAdmin.
    .AddDynamicSchemas();

// Add Persona Web Component (Server Side)
builder.Services.AddPersonaWebComponent(options =>
{
    options.Authentication.IsOtpEnabled = true;
    options.Authentication.IsExternalAuthEnabled = true;

    // Configure Google as external provider
    options.Authentication.ExternalProviders.Add(new ExternalProviderConfiguration
    {
        Id = "google",
        DisplayName = "Google",
        ChallengeUrl = "/api/auth/external/google/challenge",
        AuthenticationScheme = "Google"
    });

    options.Session.EnableRememberMe = false;
});

builder.Services.AddAuthentication(authOptions =>
    {
        authOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        authOptions.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        authOptions.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        authOptions.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "__Host-AuthSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
    })
    .AddOpenIdConnect("Google", "Sign-in with Google", options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

        options.Authority = "https://accounts.google.com/";
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;

        options.CallbackPath = "/signin-google";
        options.Scope.Add("email");
        options.Scope.Add("profile");

        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;
        options.MapInboundClaims = false;
        options.DisableTelemetry = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        options.Events.OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.SetParameter("prompt", "login");
            return Task.CompletedTask;
        };

        options.Events.OnTicketReceived = ExternalAuthenticationHandler.OnTicketReceivedAsync;
    });

builder.Services.AddDataProtection()
    .SetApplicationName("PersonaWebComponentApp")
    .PersistKeysToFileSystem(
        new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "temp")));

var app = builder.Build();

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var scope = app.Services.CreateScope();
var serviceProvider = scope.ServiceProvider;
await serviceProvider.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(cts.Token);
await serviceProvider.GetRequiredService<ISchemaAdmin>().BootstrapAsync(cts.Token);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

// Add Persona Web Component Endpoints
app.MapPersonaWebComponent("/api/auth");

await app.RunAsync();
