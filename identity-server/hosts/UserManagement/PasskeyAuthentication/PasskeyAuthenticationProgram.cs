// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage;
using Duende.Storage.PostgreSql;
using Duende.Storage.Schema;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;
using PasskeyAuthentication.TrustPolicies;
using SamplesShared;

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

builder.Services.AddHttpClient();
builder.Services.AddAuthorization();

// TODO: switch to data protection key persistence appropriate for production
builder.Services.AddDataProtection();

builder.Services.AddSingleton<FidoMdsToggle>();
builder.Services.AddSingleton<FidoMdsCache>();
builder.Services.AddTransient<FidoMdsTrustPolicy>();

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
                opt.Passkeys.RelyingPartyName = "Passkey Authentication Sample";
                opt.Passkeys.SupportedAlgorithms = [CoseAlgorithms.Es256];
                opt.Passkeys.ServerDomain = "passkey.dev.localhost";
                opt.Passkeys.AllowedOrigins = ["https://passkey.dev.localhost:6004"];

                // Attempt to exercise the "Packed" attestation format validation
                opt.Passkeys.AttestationConveyancePreference = PasskeyConstants.AttestationConveyance.Direct;

                // Restrict to platform authenticators (Windows Hello, Touch ID, Face ID):
                opt.Passkeys.AuthenticatorAttachment = PasskeyConstants.AuthenticatorAttachment.Platform;

                // Restrict to cross-platform authenticators (USB security keys, Bluetooth):
                // opt.Passkeys.AuthenticatorAttachment = PasskeyConstants.AuthenticatorAttachment.CrossPlatform;

                // Configure user verification requirement:
                opt.Passkeys.UserVerificationRequirement =
                    PasskeyConstants.UserVerificationRequirement.Required; // Must verify user (PIN, biometric)
                // opt.Passkeys.UserVerificationRequirement = PasskeyConstants.UserVerificationRequirement.Preferred; // Prefer verification (default)
                // opt.Passkeys.UserVerificationRequirement = PasskeyConstants.UserVerificationRequirement.Discouraged; // Do not verify user

                // Configure resident key (discoverable credential) requirement:
                // opt.Passkeys.ResidentKeyRequirement = PasskeyConstants.ResidentKeyRequirement.Required; // Require discoverable
                opt.Passkeys.ResidentKeyRequirement =
                    PasskeyConstants.ResidentKeyRequirement
                        .Preferred; // Prefer discoverable (default) - enables usernameless auth
                // opt.Passkeys.ResidentKeyRequirement = PasskeyConstants.ResidentKeyRequirement.Discouraged; // Discourage discoverable (username-based only)
            })
            .AddAttestationTrustPolicy<ConditionalFidoMdsTrustPolicy>()
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

// TODO: IdentityServer integration - UseIdentityServer
app.UseAuthentication();
app.UseAuthorization();
app.MapUserManagement();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.Run();
