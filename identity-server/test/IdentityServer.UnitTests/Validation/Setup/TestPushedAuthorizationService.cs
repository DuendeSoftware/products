// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;

namespace UnitTests.Validation.Setup;

/// <summary>
/// Test implementation of the pushed authorization service. Always returns a setup
/// pushed authorization request.
/// </summary>
internal class TestPushedAuthorizationService : IPushedAuthorizationService
{
    private Dictionary<string, DeserializedPushedAuthorizationRequest> pushedRequests = new();


    public Task ConsumeAsync(string referenceValue)
    {
        pushedRequests.Remove(referenceValue);
        return Task.CompletedTask;
    }

    public Task<DeserializedPushedAuthorizationRequest> GetPushedAuthorizationRequestAsync(string referenceValue)
    {
        pushedRequests.TryGetValue(referenceValue, out var par);
        return Task.FromResult(par);
    }

    public Task StoreAsync(DeserializedPushedAuthorizationRequest request)
    {
        pushedRequests[request.ReferenceValue] = request;
        return Task.CompletedTask;
    }
}
