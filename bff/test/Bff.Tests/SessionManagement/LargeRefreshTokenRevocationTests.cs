// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.SessionManagement.Revocation;
using Duende.Bff.SessionManagement.SessionStore;
using Duende.Bff.SessionManagement.TicketStore;
using Duende.Bff.Tests.TestInfra;
using Microsoft.AspNetCore.Authentication;

namespace Duende.Bff.Tests.SessionManagement;

/// <summary>
/// BFF no longer enforces any maximum length on refresh tokens it revokes on behalf of a session. That
/// refresh token comes from our own session store and is sent back to the provider that issued it, so BFF
/// uses <c>RefreshToken.Parse(value, int.MaxValue)</c> in <see cref="SessionRevocationService"/> rather than
/// ATM's default 4kb <c>UserTokenManagementOptions.MaxRefreshTokenLength</c>.
/// </summary>
public class LargeRefreshTokenRevocationTests : BffTestBase
{
    [Fact]
    public async Task server_side_session_revocation_revokes_refresh_token_longer_than_4kb_without_any_configuration()
    {
        Bff.OnConfigureBff += bff => bff.AddServerSideSessions();

        string? capturedRevocationBody = null;
        Bff.OnConfigureBffOptions += options =>
            options.BackchannelHttpHandler = new CapturingHandler(Internet, body =>
            {
                if (body.Contains("token_type_hint=refresh_token", StringComparison.Ordinal))
                {
                    capturedRevocationBody = body;
                }
            });

        await ConfigureBff(BffSetupType.V4Bff, configureOpenIdConnect: options =>
        {
            The.DefaultOpenIdConnectConfiguration(options);
            options.Scope.Add("offline_access");
        });

        Bff.BffOptions.RevokeRefreshTokenOnLogout.ShouldBeTrue();

        _ = await Bff.BrowserClient.Login();

        // Seed the session's ticket directly with a refresh token over 4kb: IdentityServer's own refresh
        // tokens are short reference handles, so this is the realistic way to exercise an oversized token.
        var bigRefreshToken = new string('r', 5 * 1024);
        var sessionStore = Bff.Resolve<IUserSessionStore>();
        var partitionKeyBuilder = Bff.Resolve<BuildUserSessionPartitionKey>();
        var partitionKey = partitionKeyBuilder();
        var sessions = await sessionStore.GetUserSessionsAsync(partitionKey, new UserSessionsFilter
        {
            SubjectId = The.Sub
        });
        var session = sessions.Single();
        var key = session.Key.ToString()!;

        var ticketStore = Bff.Resolve<IServerTicketStore>();
        var ticket = await ticketStore.RetrieveAsync(key);
        _ = ticket.ShouldNotBeNull();
        _ = ticket.Properties.UpdateTokenValue("refresh_token", bigRefreshToken);
        await ticketStore.RenewAsync(key, ticket);

        // This must not throw: before the fix, BFF would refuse to even attempt revocation of an oversized
        // refresh token (ATM's default MaxRefreshTokenLength is 4kb), so the request would never reach
        // IdentityServer.
        var revocationService = Bff.Resolve<ISessionRevocationService>();
        await revocationService.RevokeSessionsAsync(new UserSessionsFilter { SubjectId = The.Sub });

        _ = capturedRevocationBody.ShouldNotBeNull();
        capturedRevocationBody.ShouldContain(Uri.EscapeDataString(bigRefreshToken));
    }

    private sealed class CapturingHandler(HttpMessageHandler inner, Action<string> onRevocationBody) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Ct ct)
        {
            if (request.RequestUri?.AbsolutePath == "/connect/revocation" && request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(ct);
                onRevocationBody(body);
            }

            return await base.SendAsync(request, ct);
        }
    }
}
