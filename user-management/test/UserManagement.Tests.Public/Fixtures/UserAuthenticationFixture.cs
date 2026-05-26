// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Duende.IdentityModel;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.TestIsolation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Platform.UserManagement.Fixtures;

internal sealed partial class UserAuthenticationFixture(WebServerFixture webserver) : IAsyncLifetime
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception ex);

    private readonly RuntimePasskeyOriginOptions _runtimePasskeyOriginOptions = new();

    private static Ct Ct => TestContext.Current.CancellationToken;

    public Action<IUserAuthenticationBuilder> ConfigureBuilder { get; set; } = _ => { };

    public KestrelBasedTestServer App { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public HttpClient NonRedirectingClient { get; private set; } = null!;

    public string Origin { get; private set; } = null!;

    public string RelyingPartyId { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        App = new KestrelBasedTestServer("userauth", webserver, TestContext.Current.TestOutputHelper!,
            services =>
            {
                _ = services.AddAuthorization();

                _ = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(options =>
                    {
                        options.Events.OnRedirectToLogin = context =>
                        {
                            // This prevents the default redirect behavior
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        };

                        // You might also want to handle 403 Forbidden (authorized but no permission)
                        options.Events.OnRedirectToAccessDenied = context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        };
                    });
                _ = services.AddSingleton(_runtimePasskeyOriginOptions);
                _ = services.AddOptions<UserAuthenticationOptions>().Configure<RuntimePasskeyOriginOptions>((options, runtimeOptions) =>
                {
                    if (runtimeOptions.Origin is not null)
                    {
                        options.Passkeys.AllowedOrigins = [runtimeOptions.Origin];
                    }
                });

                var dbId = Guid.NewGuid();
                _ = services.AddUserManagementInternal(users =>
                {
                    _ = users.EnableAuthentication(ConfigureBuilder);
                    _ = users.AddSqliteStore(opt =>
                        opt.ConnectionString = $"Data Source=MySharedDb_{dbId};Mode=Memory;Cache=Shared");
                });
            },
            app =>
            {
                _ = app.Use(async (context, next) =>
                {
                    try
                    {
                        await next();
                    }
                    catch (Exception ex)
                    {
                        LogUnhandledException(context.RequestServices.GetRequiredService<ILogger<UserAuthenticationFixture>>(), ex);
                        throw;
                    }
                });
                _ = app.UseAuthorization();
                _ = app.MapUserManagement();

                _ = app.MapGet("/test-signin/{subjectId}", async (string subjectId, HttpContext ctx) =>
                {
                    var claims = new List<Claim> { new(JwtClaimTypes.Subject, subjectId) };
                    var identity = new ClaimsIdentity(claims, "Duende.IdentityServer", JwtClaimTypes.Name, JwtClaimTypes.Role);
                    var principal = new ClaimsPrincipal(identity);
                    await ctx.SignInAsync(principal, new AuthenticationProperties { IsPersistent = true });
                    return Results.Ok();
                });
            });

        await App.StartAsync();

        await App.GetRequiredService<IPooledStore>().MigrateAsync(TestContext.Current.CancellationToken);

        var baseUri = App.BaseAddress;
        Origin = $"{baseUri.Scheme}://{baseUri.Host}:{baseUri.Port}";
        RelyingPartyId = baseUri.Host;
        _runtimePasskeyOriginOptions.Origin = Origin;

        Client = App.CreateClient();
        NonRedirectingClient = App.CreateClient(allowAutoRedirect: false);
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();

    public async Task<(UserSubjectId SubjectId, UserName UserName, ExternalAuthenticator ExternalAuthenticator)> SeedAuthenticatorsAsync()
    {
        using var scope = App.Services.CreateScope();
        var authenticatorsSelfService = scope.ServiceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var userSelfService = scope.ServiceProvider.GetRequiredService<IUserSelfService>();

        var externalAuthenticator = TestData.CreateExternalAuthenticator();
        var userName = TestData.CreateUserName();
        var authenticators = (await authenticatorsSelfService.TryRegisterAsync(UserSubjectId.New(), externalAuthenticator, Ct)).ShouldNotBeNull();
        (await userSelfService.TrySetUserNameAsync(authenticators.SubjectId, userName, Ct)).ShouldBeTrue();
        return (authenticators.SubjectId, userName, externalAuthenticator);
    }

    public async Task<(byte[] CredentialId, ECDsa PrivateKey)> SeedPasskeyAsync(
        UserSubjectId subjectId, UserName userName, string name)
    {
        using var scope = App.Services.CreateScope();
        var passkeyAuth = scope.ServiceProvider.GetRequiredService<IPasskeyCeremonies>();
        var authenticatorsSelfService = scope.ServiceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();

        var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var session = await passkeyAuth.BeginRegistrationAsync(subjectId, userName.ToString(), "Test User", Ct);
        var credentialId = session.ChallengeId.ToByteArray();

        var clientData = WebAuthnFixtures.CreateClientDataJson(PasskeyConstants.ClientDataType.Create,
            session.Options.Challenge, Origin);
        var attestationObject =
            WebAuthnFixtures.CreateAttestationObjectWithEcdsa(PasskeyConstants.AttestationFormat.None, RelyingPartyId, credentialId, ecdsa, flags: 0x45);

        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            session.ChallengeId, clientData, attestationObject, credentialId, name);

        var result = await passkeyAuth.CompleteRegistrationAsync(request, Ct);
        var success = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        (await authenticatorsSelfService.TryAddPasskeyAsync(subjectId, success.Credential, Ct)).ShouldBeTrue();

        return (credentialId, ecdsa);
    }

    public static async Task SignInClientAsync(HttpClient client, string subjectId)
    {
        var response = await client.GetAsync($"/test-signin/{subjectId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public async Task<UserSubjectId?> GetSubjectIdAsync(UserName userName)
    {
        using var scope = App.Services.CreateScope();
        var authenticatorsAdmin = scope.ServiceProvider.GetRequiredService<IUserAuthenticatorsAdmin>();
        var user = await authenticatorsAdmin.TryGetAsync(userName, Ct);
        return user?.SubjectId;
    }

    private sealed class RuntimePasskeyOriginOptions
    {
        public string? Origin { get; set; }
    }
}
