// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.Storage;
using Duende.Storage.PostgreSql;
using Duende.Storage.Schema;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using MfaStepUp;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
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
        options.AccessDeniedPath = "/AccessDenied";
    })
    .AddCookie(MfaAuthScheme.Name, options =>
    {
        options.Cookie.Name = "__Host-MfaAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        // Short-lived: user has 5 minutes to complete the second factor
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.SlidingExpiration = false;
        options.LoginPath = "/PasskeyChallenge";
        options.AccessDeniedPath = "/AccessDenied";
    });

// Add an authorization policy "MfaRequired" to enforce using MFA to reach certain areas
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("MfaRequired", p => p
        .AddAuthenticationSchemes(MfaAuthScheme.Name, CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .RequireClaim(JwtClaimTypes.AuthenticationMethod, OidcConstants.AuthenticationMethods.MultiFactorAuthentication)
        .AddRequirements(new MaxAgeRequirement(TimeSpan.FromMinutes(5))));

builder.Services.AddTransient<IAuthorizationHandler, MaxAgeAuthorizationHandler>();
builder.Services.AddTransient<IAuthorizationMiddlewareResultHandler, StepUpAuthorizationMiddlewareResultHandler>();

// TODO: switch to data protection key persistence appropriate for production
builder.Services.AddDataProtection();

builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("pgsql")!).Build());

builder.Services
    .AddIdentityServer()
    .AddStorage(storage => storage.AddPostgreSql())
    .AddUserManagement(StorageInstanceId.Default, users =>
    {
        _ = users.Authentication(authentication => authentication
            .Configure(opt =>
            {
                // TODO: Why shouldn't we be able to configure this along with "EnablePasskeyForSecondFactor" or in a more fluent way?
                opt.Passkeys.RelyingPartyName = "MFA Step Up Sample";
                opt.Passkeys.ServerDomain = "step-up.dev.localhost";
                opt.Passkeys.AllowedOrigins = ["https://step-up.dev.localhost:6007"];
            })
            .EnablePasskeyForSecondFactor<SampleSecondFactorPasskeyResolver>()
            .UseSmtpOtpDispatcher(options => builder.Configuration.GetSection("Smtp").Bind(options)));
    });

// OtpCookie adds the ability to store OTP-related data in a secure cookie between requests
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

app.UseAuthentication();
app.UseAuthorization();

app.MapUserManagement();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();

app.Run();
