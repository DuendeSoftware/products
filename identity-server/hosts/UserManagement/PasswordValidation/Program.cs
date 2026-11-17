// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage;
using Duende.Storage.PostgreSql;
using Duende.Storage.Schema;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;
using PasswordValidation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Configure Authentication with Cookie-based sessions
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

// TODO: switch to data protection key persistence appropriate for production
builder.Services.AddDataProtection();

// Register the HaveIBeenPwned HTTP client
builder.Services.AddHttpClient("hibp", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("password-validation-sample/1.0");
});

builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("pgsql")!).Build());

builder.Services
    .AddIdentityServer()
    .AddStorage(storage => storage.AddPostgreSql())
    .AddUserManagement(StorageInstanceId.Default, users =>
    {
        _ = users.Authentication(authentication =>
        {
            // Register multiple password validators — all must pass for a password to be accepted.
            // Validators run in registration order; the first rejection short-circuits.
            _ = authentication
                .AddPasswordValidator<BlocklistPasswordValidator>()
                .AddPasswordValidator<HaveIBeenPwnedPasswordValidator>()
                .AddPasswordValidator<PatternPasswordValidator>();
        });
    });

var app = builder.Build();

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// TODO: consider bootstrapping in a separate, one-off process
using var scope = app.Services.CreateScope();
await scope.ServiceProvider.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(cts.Token);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler("/Error");
    _ = app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

await app.RunAsync();
