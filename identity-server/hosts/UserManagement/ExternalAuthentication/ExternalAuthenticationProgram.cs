// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.PostgreSql;
using Duende.Storage.Schema;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Profiles;
using ExternalAuthentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages()
    .AddRazorRuntimeCompilation();

// Configure Authentication with Cookie-based sessions
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
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
    .AddOpenIdConnect("duende", "Duende IdentityServer", options =>
    {
        options.Authority = "https://demo.duendesoftware.com";
        options.RequireHttpsMetadata = false;

        options.ClientId = "interactive.confidential";
        options.ClientSecret = "secret";

        // code flow + PKCE (PKCE is turned on by default)
        options.ResponseType = "code";
        options.UsePkce = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("offline_access");

        ConfigureOpenIdConnectDefaults(options);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    })
    .AddOpenIdConnect("Google", "Sign-in with Google", options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        //options.ForwardSignOut = IdentityServerConstants.DefaultCookieAuthenticationScheme;

        options.Authority = "https://accounts.google.com/";
        options.ClientId = "708778530804-rhu8gc4kged3he14tbmonhmhe7a43hlp.apps.googleusercontent.com";

        options.CallbackPath = "/signin-google";
        options.Scope.Add("email");
        options.Scope.Add("profile");

        ConfigureOpenIdConnectDefaults(options);

        options.Events.OnRedirectToIdentityProvider = context =>
        {
            context.ProtocolMessage.SetParameter("prompt", "login");
            return Task.CompletedTask;
        };

    })
    ;
builder.Services.AddAuthorization();

// Configure Data Protection with secure defaults
builder.Services.AddDataProtection()
    .SetApplicationName("ExternalAuthentication")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "temp")));

// Add and configure Persona
builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("pgsql")!).Build());

builder.Services
    .AddIdentityServer()
    .AddStorage(storage => storage.AddPostgreSql())
    .AddUserManagement(StorageInstanceId.Default, users =>
    {
        users.Authentication(persona => persona.UseSmtpOtpDispatcher(options =>
            {
                builder.Configuration.GetSection("Smtp").Bind(options);
            }));
    })
    // This sample creates its profile schema at startup through ISchemaAdmin.
    .AddDynamicSchemas();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Persona
using var scope = app.Services.CreateScope();
await scope.ServiceProvider.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(cts.Token);
await scope.ServiceProvider.GetRequiredService<ISchemaAdmin>().BootstrapAsync(cts.Token);

// Configure the HTTP request pipeline.
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

app.Run();

static void ConfigureOpenIdConnectDefaults(OpenIdConnectOptions options)
{
    // not mapped by default
    // keeps id_token smaller
    options.GetClaimsFromUserInfoEndpoint = true;
    // save tokens in cookie
    options.SaveTokens = true;
    // disable MS auto claim type renaming
    options.MapInboundClaims = false;
    //Disable x-client-SKU and x-client-ver headers (security issue)
    options.DisableTelemetry = true;


    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "name",
        RoleClaimType = "role"
    };

    options.Events.OnTicketReceived += ExternalAuthenticationHandler.OnTicketReceivedAsync;

}


internal static class UserAttributes
{
    internal static readonly AttributeDefinition Name = new()
    {
        Code = AttributeCode.Create("name"),
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create("User's name.")
    };

    internal static async Task BootstrapAsync(this ISchemaAdmin schemaAdmin, Ct ct)
    {
        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, ct);
        var schema = getResult.Found
            ? getResult.Item!
            : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };

        schema.AttributeDefinitions.Add(Name);

        if (getResult.Found)
        {
            await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, ct);
        }
        else
        {
            await schemaAdmin.CreateAsync(schema, ct);
        }
    }
}
