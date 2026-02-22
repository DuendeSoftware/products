// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class MockPersistedGrantService : IPersistedGrantService
{
    public IEnumerable<Grant> GetAllGrantsResult { get; set; }
    public bool RemoveAllGrantsWasCalled { get; set; }

    public Task<IEnumerable<Grant>> GetAllGrantsAsync(string subjectId, Ct ct) => Task.FromResult(GetAllGrantsResult ?? Enumerable.Empty<Grant>());

    public Task RemoveAllGrantsAsync(string subjectId, Ct ct, string clientId = null, string sessionId = null)
    {
        RemoveAllGrantsWasCalled = true;
        return Task.CompletedTask;
    }
}
