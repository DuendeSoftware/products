// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;

namespace UnitTests.Common;

public class MockUserSession : IUserSession
{
    public List<string> Clients = new List<string>();
    public List<SamlSpSessionData> SamlSessions = new List<SamlSpSessionData>();

    public bool EnsureSessionIdCookieWasCalled { get; set; }
    public bool RemoveSessionIdCookieWasCalled { get; set; }
    public bool CreateSessionIdWasCalled { get; set; }

    public ClaimsPrincipal User { get; set; }
    public string SessionId { get; set; }
    public AuthenticationProperties Properties { get; set; }


    public Task<string> CreateSessionIdAsync(ClaimsPrincipal principal, AuthenticationProperties properties, Ct _)
    {
        CreateSessionIdWasCalled = true;
        User = principal;
        SessionId = Guid.NewGuid().ToString();
        return Task.FromResult(SessionId);
    }

    public Task<ClaimsPrincipal> GetUserAsync(Ct _) => Task.FromResult(User);

    Task<string> IUserSession.GetSessionIdAsync(Ct _) => Task.FromResult(SessionId);

    public Task EnsureSessionIdCookieAsync(Ct _)
    {
        EnsureSessionIdCookieWasCalled = true;
        return Task.CompletedTask;
    }

    public Task RemoveSessionIdCookieAsync(Ct _)
    {
        RemoveSessionIdCookieWasCalled = true;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetClientListAsync(Ct _) => Task.FromResult<IEnumerable<string>>(Clients);

    public Task AddClientIdAsync(string clientId, Ct _)
    {
        Clients.Add(clientId);
        return Task.CompletedTask;
    }

    public Task AddSamlSessionAsync(SamlSpSessionData session, Ct _)
    {
        SamlSessions.RemoveAll(s => s.EntityId == session.EntityId);
        SamlSessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<SamlSpSessionData>> GetSamlSessionListAsync(Ct _) => Task.FromResult<IEnumerable<SamlSpSessionData>>(SamlSessions);

    public Task RemoveSamlSessionAsync(string entityId, Ct _)
    {
        SamlSessions.RemoveAll(s => s.EntityId == entityId);
        return Task.CompletedTask;
    }
}
