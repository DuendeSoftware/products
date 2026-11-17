// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.PostgreSql;
using Duende.Storage.Schema;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using TotpAuthentication;

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

        // When in the 'registration process', we sign in with an unauthenticated principal.
        // This allows us to carry state through the registration steps without having
        // to fully authenticate the user.
        options.RequireAuthenticatedSignIn = false;
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
    });

builder.Services.AddAuthorization();

// Configure Data Protection with secure defaults
builder.Services.AddDataProtection()
    .SetApplicationName("PersonaTOTPApp")
    .PersistKeysToFileSystem(
        new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "temp")));

// Add and configure Persona
builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("pgsql")!).Build());

builder.Services
    .AddIdentityServer()
    .AddStorage(storage => storage.AddPostgreSql())
    .AddUserManagement(StorageInstanceId.Default, users =>
    {
        users.Authentication(persona =>
        {
            persona.Configure(options =>
            {
                options.Passwords.MaxLength = 100;
                options.Passwords.MinLength = 6;
                options.Passwords.MinLower = 0;
                options.Passwords.MinDigits = 0;
                options.Passwords.MinSymbols = 0;
                options.Passwords.MinUpper = 0;
            });
            persona.UseSmtpOtpDispatcher(options =>
            {
                builder.Configuration.GetSection("Smtp").Bind(options);
            });
        });
    })
    // This sample creates its profile schema at startup through ISchemaAdmin.
    .AddDynamicSchemas();

// Cookie Services for storing authentication state between requests
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<CookieAuthenticationStateService>();
builder.Services.AddSingleton<OtpCookieService>();
builder.Services.AddScoped<RegistrationManager>();
builder.Services.AddScoped<TotpAuthenticatorService>();

var app = builder.Build();

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Persona - Initialize schema
using var scope = app.Services.CreateScope();
var serviceProvider = scope.ServiceProvider;
await serviceProvider.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(cts.Token);
await serviceProvider.GetRequiredService<ISchemaAdmin>().BootstrapAsync(cts.Token);

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
