// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace UnitTests.Common;

//class MockClientSessionService : IClientSessionService
//{
//    public List<string> Clients = new List<string>();

//    public bool RemoveCookieWasCalled { get; private set; }

//    public Task AddClientIdAsync(string clientId)
//    {
//        Clients.Add(clientId);
//        return Task.CompletedTask;
//    }

//    public Task EnsureClientListCookieAsync(string sid)
//    {
//        return Task.CompletedTask;
//    }

//    public Task<IEnumerable<string>> GetClientListAsync()
//    {
//        return Task.FromResult<IEnumerable<string>>(Clients);
//    }

//    public IEnumerable<string> GetClientListFromCookie(string sid)
//    {
//        return Clients;
//    }

//    public void RemoveCookie(string sid)
//    {
//        RemoveCookieWasCalled = true;
//        Clients.Clear();
//    }
//}
