// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.Storage;
using Duende.Storage.PostgreSql;
using Duende.Storage.Schema;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;
using PasskeyAuthenticationAs2FA;
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
    .AddCookie(PartialAuthScheme.Name, options =>
    {
        options.Cookie.Name = "__Host-PartialAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        // Short-lived: user has 5 minutes to complete the second factor
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.SlidingExpiration = false;
        options.LoginPath = "/Login";
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("2fa", policy =>
        policy.RequireClaim(JwtClaimTypes.AuthenticationMethod,
            OidcConstants.AuthenticationMethods.OneTimePassword));
});

builder.Services.AddDataProtection();

builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("pgsql")!).Build());

_ = builder.Services
    // TODO: IdentityServer integration - AddDeveloperSigningCredential, EnableIdentityServer
    .AddIdentityServer()
    .AddStorage(storage => storage.AddPostgreSql())
    .AddUserManagement(StorageInstanceId.Default, users =>
    {
        _ = users.Authentication(authentication => authentication
            .Configure(opt =>
            {
                opt.Passkeys.RelyingPartyName = "Passkey Authentication as 2FA Sample";
                opt.Passkeys.ServerDomain = "passkey-2fa.dev.localhost";
                opt.Passkeys.AllowedOrigins = ["https://passkey-2fa.dev.localhost:6006"];
                opt.Passkeys.AttestationConveyancePreference = PasskeyConstants.AttestationConveyance.Direct;
            })
            .EnablePasskeyForSecondFactor<SampleSecondFactorPasskeyResolver>()
            .UseSmtpOtpDispatcher(options => builder.Configuration.GetSection("Smtp").Bind(options)));
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<OtpCookie>();

var app = builder.Build();

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var scope = app.Services.CreateScope();
await scope.ServiceProvider.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(cts.Token);

if (!app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler("/Error");
    _ = app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

// TODO: IdentityServer integration - UseIdentityServer
app.UseAuthentication();
app.UseAuthorization();

app.MapUserManagement();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.Run();
