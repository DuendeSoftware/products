// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;
using System.Security.Claims;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.Blazor;
using Duende.Bff.Otel;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.SessionManagement.TicketStore;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Bff.Tests.Blazor;

public class ServerSideTokenStoreTests
{
    public TestData The = new TestData();

    private ClaimsPrincipal CreatePrincipal(string sub, string sid) => new(new ClaimsIdentity([
        new Claim("sub", sub),
        new Claim("sid", sid)
    ], "pwd", "name", "role"));

    [Fact]
    public async Task Can_add_retrieve_and_remove_tokens()
    {
        var user = CreatePrincipal("sub", "sid");
        var props = new AuthenticationProperties();
        var expectedToken = new UserToken
        {
            AccessToken = AccessToken.Parse("expected-access-token"),
            Expiration = DateTime.Now.AddHours(1),
            AccessTokenType = AccessTokenType.Parse("Bearer"),
            ClientId = ClientId.Parse("some_client"),
        };

        // Create shared dependencies
        var sessionStore =
            new InMemoryUserSessionStore(new NullLogger<InMemoryUserSessionStore>());
        var dataProtection = new EphemeralDataProtectionProvider();

        // Use the ticket store to save the user's initial session
        // Note that we don't yet have tokens in the session
        var sessionService = new ServerSideTicketStore(
            metrics: new BffMetrics(new DummyMeterFactory()),
            timeProvider: new FakeTimeProvider(),
            store: sessionStore,
            dataProtectionProvider: dataProtection,
            partitionKeyBuilder: () => The.PartitionKey,
            accessor: new FakeHttpContextAccessor()
            {
                HttpContext = new DefaultHttpContext()
            },
            logger: new NullLogger<ServerSideTicketStore>());
        await sessionService.StoreAsync(new AuthenticationTicket(
            user,
            props,
            "test"
        ));

        var tokensInProps = new MockStoreTokensInAuthProps()
        {
            RefreshToken = new UserRefreshToken(RefreshToken.Parse("some-refresh-token"), null)
        };

        var sut = new ServerSideTokenStore(
            tokensInAuthProperties: tokensInProps,
            sessionStore: sessionStore,
            dataProtectionProvider: dataProtection,
            userSessionPartitionKeyBuilder: () => The.PartitionKey,
            logger: new NullLogger<ServerSideTokenStore>(),
            authenticationStateProvider: Substitute.For<AuthenticationStateProvider, IHostEnvironmentAuthenticationStateProvider>());


        await sut.StoreTokenAsync(user, expectedToken);
        var tokenForParameters = await sut.GetTokenAsync(user).GetToken();
        var actualToken = tokenForParameters.TokenForSpecifiedParameters;

        actualToken.ShouldNotBeNull();
        actualToken.AccessToken.ShouldBe(expectedToken.AccessToken);

        await sut.ClearTokenAsync(user);

        var resultAfterClearing = await sut.GetTokenAsync(user)
            .GetToken();
        resultAfterClearing.TokenForSpecifiedParameters.ShouldBeNull();
    }

    private class MockStoreTokensInAuthProps : IStoreTokensInAuthenticationProperties
    {
        public UserToken? Stored;
        public UserRefreshToken? RefreshToken;

        public TokenResult<TokenForParameters> GetUserToken(AuthenticationProperties authenticationProperties,
            UserTokenRequestParameters? parameters = null)
        {
            // Return a successful TokenResult<TokenForParameters> if a token is stored, otherwise unsuccessful
            if (Stored != null)
            {
                return new TokenForParameters(Stored, null);
            }

            if (RefreshToken != null)
            {
                return new TokenForParameters(RefreshToken);
            }

            return TokenResult.Failure("No token stored");
        }


        public Task SetUserTokenAsync(UserToken token, AuthenticationProperties authenticationProperties,
            UserTokenRequestParameters? parameters = null, CancellationToken ct = new CancellationToken())
        {
            Stored = token;
            return Task.CompletedTask;
        }

        public void RemoveUserToken(AuthenticationProperties authenticationProperties,
            UserTokenRequestParameters? parameters = null) => Stored = null;

        public Task<Scheme> GetSchemeAsync(UserTokenRequestParameters? parameters = null,
            CancellationToken ct = new CancellationToken()) =>
            Task.FromResult(Scheme.Bearer);
    }

    private class DummyMeterFactory : IMeterFactory
    {
        public void Dispose()
        {
        }

        public Meter Create(MeterOptions options) => new Meter(options);
    }
}

public static class TokenResultExtensions
{
    /// <summary>
    /// Convenience method to extract the token from an asynchronous TokenResult.
    /// You can now do: GetTokenAsync(ct).GetToken().
    /// Note, this method will throw an InvalidOperationException with the token failure
    /// if the token result was not successful.
    /// </summary>
    /// <typeparam name="T">Token</typeparam>
    /// <param name="task">The task that retrieved the token. </param>
    /// <returns>Token if successful</returns>
    /// <exception cref="InvalidOperationException">Thrown if the token was not retrieved successfully. </exception>
    public static async Task<T> GetToken<T>(this Task<TokenResult<T>> task) where T : class
    {
        var result = await task;

        if (!result.WasSuccessful(out var token, out var failure))
        {
            throw new InvalidOperationException($"Failed to get token: {failure}");
        }

        return token;
    }
}
