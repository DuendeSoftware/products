// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.TestIsolation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityServer8.UnitTests;

public sealed class SigninTests(WebServerFixture testServer) : IAsyncDisposable
{
    private KestrelBasedTestServer _identityServer = null!;
    private KestrelBasedTestServer _clientApp = null!;
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Can_sign_in_using_otp()
    {
        _clientApp = SetupClientApp();
        _identityServer = SetupIdentityServer(webapp =>
        {
            // The following code signs in a user using a OTP code, which is the main focus of this test.
            _ = webapp.MapGet("/account/login",
                async (HttpContext c, IOtpSender otpSender, IOtpAuthenticator otpAuthenticator,
                    IOtpDispatcher dispatcher) =>
                {
                    var sendResult = (await otpSender.TrySendOtpAsync(
                            new OtpAddress(OtpChannel.Email, (EmailAddress)"test@example.com"), Ct))
                        .ShouldBeOfType<SendOtpResult.Sent>();

                    var otp = ((FakeOtpDispatcher)dispatcher).Calls.Single().Otp;

                    var authResult = (await otpAuthenticator.TryAuthenticateAsync(otp, sendResult.Token, Ct))
                        .ShouldBeOfType<OtpAuthenticationResult.Success>();

                    await c.SignInAsync(new IdentityServerUser(authResult.UserSubjectId.Value));

                    var returnUrl = c.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
                    c.Response.Redirect(returnUrl);
                });
        });

        await _identityServer.StartAsync();
        await _clientApp.StartAsync();

        await _identityServer.GetRequiredService<IDatabaseSchema>().MigrateAsync(Ct);

        var client = _clientApp.CreateClient(allowAutoRedirect: true);

        var result = await client.GetAsync("/login");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadAsStringAsync()).ShouldBe("Ok");
    }

    [Fact]
    public async Task Can_sign_in_using_passkey()
    {
        var runtimeOrigin = new RuntimePasskeyOrigin();

        _clientApp = SetupClientApp();
        _identityServer = SetupIdentityServer(webapp =>
        {
            _ = webapp.MapGet("/account/login",
                async (
                    HttpContext httpContext,
                    IExternalAuthenticator externalAuthenticator,
                    IPasskeyCeremonies passkeyAuth,
                    IUserAuthenticatorsSelfService authenticatorsSelfService) =>
                {
                    // Register a user with a passkey
                    var externalAuth = new ExternalAuthenticatorAddress(
                        ExternalAuthenticatorName.Create("test-ext"),
                        EmailAddress.Create("test@example.com"));
                    var subjectId = (await externalAuthenticator.TryAuthenticateAsync(externalAuth, Ct))
                        .ShouldBeOfType<ExternalAuthenticationResult.Success>().UserSubjectId;

                    using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                    var origin = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
                    var rpId = httpContext.Request.Host.Host;

                    // Begin + complete registration
                    var regSession = await passkeyAuth.BeginRegistrationAsync(subjectId, "user@example.com", "Test User", Ct);
                    var credentialId = regSession.ChallengeId.ToByteArray();
                    var regClientData = WebAuthnFixtures.CreateClientDataJson(
                        PasskeyConstants.ClientDataType.Create, regSession.Options.Challenge, origin);
                    var attestationObject = WebAuthnFixtures.CreateAttestationObjectWithEcdsa(
                        PasskeyConstants.AttestationFormat.None, rpId, credentialId, ecdsa, flags: 0x45);
                    var regRequest = WebAuthnFixtures.CreateCompleteRegistrationRequest(
                        regSession.ChallengeId, regClientData, attestationObject, credentialId, "Test Passkey");
                    var regResult = (await passkeyAuth.CompleteRegistrationAsync(regRequest, Ct))
                        .ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
                    (await authenticatorsSelfService.TryAddPasskeyAsync(subjectId, regResult.Credential, Ct)).ShouldBeTrue();

                    // Begin + complete authentication
                    var authBegin = (await passkeyAuth.BeginAuthenticationAsync(Ct))
                        .ShouldBeOfType<PasskeyAuthenticationBeginResult.Success>();
                    var authClientData = WebAuthnFixtures.CreateClientDataJson(
                        PasskeyConstants.ClientDataType.Get, authBegin.Session.Options.Challenge, origin);
                    var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData(rpId, flags: 0x01, signCount: 1);
                    var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(authClientData);
                    var signature = WebAuthnFixtures.CreateValidSignature(ecdsa, authenticatorData, clientDataBytes);

                    var authRequest = new PasskeyCompleteAuthenticationRequest
                    {
                        ChallengeId = authBegin.Session.ChallengeId,
                        Id = Base64Url.EncodeToString(credentialId),
                        RawId = Base64Url.EncodeToString(credentialId),
                        Type = PasskeyConstants.CredentialType.PublicKey,
                        Response = new AuthenticatorAssertionResponse
                        {
                            ClientDataJSON = authClientData,
                            AuthenticatorData = Base64Url.EncodeToString(authenticatorData),
                            Signature = signature
                        }
                    };

                    var authResult = (await passkeyAuth.CompleteAuthenticationAsync(authRequest, Ct))
                        .ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();

                    await httpContext.SignInAsync(new IdentityServerUser(authResult.UserSubjectId.Value));

                    var returnUrl = httpContext.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
                    httpContext.Response.Redirect(returnUrl);
                });
        }, services =>
        {
            _ = services.AddSingleton(runtimeOrigin);
            _ = services.AddOptions<UserAuthenticationOptions>()
                .Configure<RuntimePasskeyOrigin>((options, runtime) =>
                {
                    if (runtime.Origin is not null)
                    {
                        options.Passkeys.AllowedOrigins = [runtime.Origin];
                    }
                });
        });

        await _identityServer.StartAsync();
        runtimeOrigin.Origin = _identityServer.BaseAddress.ToString().TrimEnd('/');

        await _clientApp.StartAsync();

        await _identityServer.GetRequiredService<IDatabaseSchema>().MigrateAsync(Ct);

        var client = _clientApp.CreateClient(allowAutoRedirect: true);

        var result = await client.GetAsync("/login");
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await result.Content.ReadAsStringAsync()).ShouldBe("Ok");
    }

    private KestrelBasedTestServer SetupIdentityServer(Action<WebAppWrapper> configureWebApp, Action<IServiceCollection>? configureExtraServices = null)
    {
        var identityServer = new KestrelBasedTestServer(
            serverName: "identity-server",
            fixture: testServer,
            output: TestContext.Current.TestOutputHelper!,
            configureServices: services =>
            {
                _ = services.AddLogging(logging => logging.AddXUnit(TestContext.Current.TestOutputHelper!));
                _ = services.AddIdentityServer(opt => { opt.UserInteraction.LoginUrl = "/account/login"; })
                    .AddUserManagement(users =>
                    {
                        _ = users.AddSqliteInMemoryStore();
                        _ = users.Authentication(auth => auth.UseOtpDispatcher<FakeOtpDispatcher>());
                    })
                    .AddInMemoryIdentityResources([
                        new IdentityResources.OpenId(),
                        new IdentityResources.Profile()
                    ])
                    .AddInMemoryClients([
                        new Client()
                        {
                            ClientId = "test-client",
                            ClientSecrets = [new Secret("secret".Sha256())],
                            AllowedGrantTypes = GrantTypes.Code,
                            RequirePkce = true,
                            RedirectUris = { _clientApp.BuildUrl("signin-oidc").ToString() },
                            PostLogoutRedirectUris = { _clientApp.BuildUrl("signout-callback-oidc").ToString() },
                            FrontChannelLogoutUri = _clientApp.BuildUrl("signout-oidc").ToString(),
                            AllowedScopes =
                            {
                                IdentityServerConstants.StandardScopes.OpenId,
                                IdentityServerConstants.StandardScopes.Profile,
                            }
                        }
                    ]);
                configureExtraServices?.Invoke(services);
            },
            configurePipeline: webapp =>
            {
                _ = webapp.Use(async (c, n) =>
                {
                    try
                    {
                        await n();
                    }
                    catch (Exception ex)
                    {
#pragma warning disable CA1848 // Use the LoggerMessage delegates
                        c.RequestServices.GetRequiredService<ILogger<SigninTests>>().LogError(ex, "An error occurred");
#pragma warning restore CA1848
                        throw;
                    }
                });

                _ = webapp.UseIdentityServer();
                configureWebApp(webapp);
            });

        return identityServer;
    }

    private KestrelBasedTestServer SetupClientApp() =>
        new(
            serverName: "client-server",
            fixture: testServer,
            output: TestContext.Current.TestOutputHelper!,
            configureServices: services =>
            {
                _ = services.AddLogging(logging => logging.AddXUnit(TestContext.Current.TestOutputHelper!));
                _ = services.AddAuthentication(opt =>
                    {
                        opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                        opt.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                    })
                    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
                    {
                        options.Authority = _identityServer.BaseAddress.ToString();
                        options.ClientId = "test-client";
                        options.ClientSecret = "secret";
                        options.ResponseType = "code";
                        options.ResponseMode = "query";
                        options.MapInboundClaims = false;
                        options.SaveTokens = true;
                        options.DisableTelemetry = true;
                    });
            },
            configurePipeline: webapp =>
            {
                _ = webapp.MapGet("/", () => "Ok");
                _ = webapp.MapGet(
                    "/login",
                    async httpContext =>
                        await httpContext.ChallengeAsync(new AuthenticationProperties { RedirectUri = "/" }));
            });

    public async ValueTask DisposeAsync()
    {
        await _identityServer.DisposeAsync();
        await _clientApp.DisposeAsync();
    }
}

internal sealed class RuntimePasskeyOrigin
{
    public string? Origin { get; set; }
}
