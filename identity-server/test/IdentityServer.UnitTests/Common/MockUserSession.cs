// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;

namespace UnitTests.Common;

public class MockUserSession : IUserSession
{
    public List<string> Clients = new List<string>();

    public bool EnsureSessionIdCookieWasCalled { get; set; }
    public bool RemoveSessionIdCookieWasCalled { get; set; }
    public bool CreateSessionIdWasCalled { get; set; }

    public ClaimsPrincipal User { get; set; }
    public string SessionId { get; set; }
    public AuthenticationProperties Properties { get; set; }


    public Task<string> CreateSessionIdAsync(ClaimsPrincipal principal, AuthenticationProperties properties)
    {
        CreateSessionIdWasCalled = true;
        User = principal;
        SessionId = Guid.NewGuid().ToString();
        return Task.FromResult(SessionId);
    }

    public Task<ClaimsPrincipal> GetUserAsync() => Task.FromResult(User);

    Task<string> IUserSession.GetSessionIdAsync() => Task.FromResult(SessionId);

    public Task EnsureSessionIdCookieAsync()
    {
        EnsureSessionIdCookieWasCalled = true;
        return Task.CompletedTask;
    }

    public Task RemoveSessionIdCookieAsync()
    {
        RemoveSessionIdCookieWasCalled = true;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetClientListAsync() => Task.FromResult<IEnumerable<string>>(Clients);

    public Task AddClientIdAsync(string clientId)
    {
        Clients.Add(clientId);
        return Task.CompletedTask;
    }
}
