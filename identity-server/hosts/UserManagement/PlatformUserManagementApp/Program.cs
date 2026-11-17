// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;
using PlatformUserManagementApp;
using PlatformUserManagementApp.Import;
using SamplesShared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "__Host-PlatformMgmtSession";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

builder.Services.AddAuthorization();
builder.Services.AddDataProtection();
builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("pgsql")!).Build());

_ = builder.Services
    .AddIdentityServer()
    .AddStorage(storage =>
        storage.AddSqlite(opt => opt.ConnectionString = $"Data Source=data;Mode=Memory;Cache=Shared"))
    .AddUserManagement(StorageInstanceId.Default, users =>
    {
        _ = users.Authentication(authentication =>
            {
                _ = authentication.Configure(opt =>
                    {
                        opt.Passkeys.ServerDomain = "localhost";
                        opt.Passkeys.AllowedOrigins = ["https://localhost:6007"];
                        opt.Passkeys.RelyingPartyName = "Platform User Management Sample";
                        opt.Passkeys.AttestationConveyancePreference = PasskeyConstants.AttestationConveyance.Direct;
                    });
                _ = authentication.UseSmtpOtpDispatcher(options => builder.Configuration.GetSection("Smtp").Bind(options));
                _ = authentication.AddPasswordHashAlgorithm<AspNetIdentityPasswordHashAlgorithm>();
            });
    })
    // This sample edits its profile schema at runtime through ISchemaAdmin (see Pages/Schema).
    .AddDynamicSchemas();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<OtpCookie>();
builder.Services.AddSingleton<Duende.UserManagement.Import.IUserImportConflictResolver, OverwriteConflictResolver>();
builder.Services.AddTransient<PlatformUserManagementApp.Import.IUserImporter, AspNetIdentityImporter>();

var app = builder.Build();

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(cts.Token);
    var schemaAdmin = scope.ServiceProvider.GetRequiredService<ISchemaAdmin>();
    await ScimSchema.SeedAsync(schemaAdmin, cts.Token);
    await SeedData.SeedAsync(scope.ServiceProvider, cts.Token);
}

if (!app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler("/Error");
    _ = app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapUserManagement();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();
