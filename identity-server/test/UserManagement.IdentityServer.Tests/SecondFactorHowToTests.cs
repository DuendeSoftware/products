// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Import;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer8.UnitTests;

// Not a snippet: shared by the test class and FirstFactorSubjectResolver below.
internal static class FirstFactorScheme
{
    public const string Name = "FirstFactor";
}

public sealed class SecondFactorHowToTests
{
    private static Ct Ct => TestContext.Current.CancellationToken;

    // Not a snippet: registers a cookie scheme standing in for however a login page tracks a
    // partially-authenticated (first-factor-only) user, and a test-only endpoint that signs into
    // it, so FirstFactorSubjectResolver below has a realistic HttpContext.User to read from.
    private static void RegisterFirstFactorScheme(WebApplicationBuilder builder)
    {
        _ = builder.Services.AddHttpContextAccessor();
        _ = builder.Services.AddAuthentication()
            .AddCookie(FirstFactorScheme.Name);
    }

    private static void MapTestOnlyFirstFactorSignIn(WebApplication app) =>
        app.MapPost("/__test/first-factor-signin", async (HttpContext httpContext, string sub) =>
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", sub)], FirstFactorScheme.Name));
            await httpContext.SignInAsync(FirstFactorScheme.Name, principal);
            return Results.Ok();
        });

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithPasskeySecondFactor(WebApplicationBuilder builder)
    {
        RegisterFirstFactorScheme(builder);

        // begin-snippet: mfa-enable-passkey-second-factor
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(
                auth => auth.EnablePasskeyForSecondFactor<FirstFactorSubjectResolver>()));
        // end-snippet
    }
#pragma warning restore IDE0022

    private static void Register(WebApplicationBuilder builder) =>
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(_ => { });

    private static void MapEndpoints(WebApplication app) => _ = app.MapUserManagement();

    [Fact]
    public async Task Second_factor_passkey_begin_endpoint_is_not_mapped_without_a_resolver()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        Register(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        MapEndpoints(app);

        await app.StartAsync(Ct);
        using var client = app.GetTestClient();

        var response = await client.PostAsync("/passkeys/authenticate/begin", null, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Second_factor_passkey_begin_endpoint_rejects_the_request_when_the_resolver_finds_no_first_factor_subject()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithPasskeySecondFactor(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        _ = app.UseAuthentication();
        MapEndpoints(app);
        MapTestOnlyFirstFactorSignIn(app);

        await app.StartAsync(Ct);
        using var client = app.GetTestClient();

        // No first-factor sign-in happened, so FirstFactorSubjectResolver resolves no subject.
        var response = await client.PostAsync("/passkeys/authenticate/begin", null, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Second_factor_passkey_begin_endpoint_succeeds_for_a_user_signed_in_with_the_first_factor()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithPasskeySecondFactor(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        _ = app.UseAuthentication();
        MapEndpoints(app);
        MapTestOnlyFirstFactorSignIn(app);

        await app.StartAsync(Ct);
        using var client = app.GetTestClient();

        var subjectId = UserSubjectId.New();

        // Create the user record directly through the public import API (no profile schema needed
        // for this test), then create the authenticators record and register a passkey for it
        // through the public admin API: a real registration ceremony needs a browser's WebAuthn
        // API, which these registration-only tests don't simulate (see the usage how-to for a full
        // registration-and-authentication ceremony).
        var importer = app.Services.GetRequiredService<IUserImporter>();
        _ = await importer.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = ValidatedAttributeValueCollection.Empty }],
            Ct);

        var userAdmin = app.Services.GetRequiredService<IUserAdmin>();
        _ = (await userAdmin.Authenticators.TryAddAsync(subjectId, [], [], Ct)).ShouldNotBeNull();

        var selfService = app.Services.GetRequiredService<IUserAuthenticatorsSelfService>();
        var passkeyAdded = await selfService.TryAddPasskeyAsync(
            subjectId,
            new PasskeyCredentialData(
                PasskeyCredentialId.From(Guid.NewGuid().ToByteArray()),
                new byte[] { 1, 2, 3 },
                CoseAlgorithms.Es256,
                SignCount: 0,
                BackupEligible: false,
                BackedUp: false,
                Guid.Empty,
                DateTimeOffset.UtcNow,
                "My Passkey",
                Transports: null),
            Ct);
        passkeyAdded.ShouldBeTrue();

        // Simulate the login page's first-factor sign-in, then pass the resulting cookie along
        // with the second-factor begin request.
        var signInResponse = await client.PostAsync(
            $"/__test/first-factor-signin?sub={Uri.EscapeDataString(subjectId.Value)}", null, Ct);
        _ = signInResponse.EnsureSuccessStatusCode();
        var cookie = signInResponse.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.First().Split(';')[0]
            : throw new InvalidOperationException("No Set-Cookie header in the sign-in response.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/passkeys/authenticate/begin");
        request.Headers.Add("Cookie", cookie);
        var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

// Not a snippet: this project has no access to the usage how-to's Func-based FirstFactorSubjectResolver
// stand-in, so the registration test implements the same resolver name reading IHttpContextAccessor
// instead — one of the realistic sources named in the how-to.
internal sealed class FirstFactorSubjectResolver(IHttpContextAccessor httpContextAccessor) : ISecondFactorPasskeyAuthenticationResolver
{
    public async Task<UserSubjectId?> ResolveAsync(Ct ct)
    {
        if (httpContextAccessor.HttpContext is not { } httpContext)
        {
            return null;
        }

        var result = await httpContext.AuthenticateAsync(FirstFactorScheme.Name);
        var subject = result.Succeeded ? result.Principal.FindFirst("sub")?.Value : null;
        return UserSubjectId.CreateOrDefault(subject);
    }
}
