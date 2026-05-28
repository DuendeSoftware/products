// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Saml;

namespace UnitTests.Common;

internal sealed class SpySamlSigninStateStore(SamlAuthenticationState? retrieveReturns = null) : ISamlSigninStateStore
{
    public SamlAuthenticationState? CapturedState { get; private set; }
    public Guid? CapturedStateId { get; private set; }
    public int RetrieveCallCount { get; private set; }
    public int RemoveCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public SamlAuthenticationState? LastUpdatedState { get; private set; }
    public Guid? LastUpdatedStateId { get; private set; }

    public Task<Guid> StoreSigninRequestStateAsync(SamlAuthenticationState state, Ct ct)
    {
        CapturedState = state;
        var id = Guid.NewGuid();
        CapturedStateId = id;
        return Task.FromResult(id);
    }

    public Task<SamlAuthenticationState?> RetrieveSigninRequestStateAsync(Guid stateId, Ct ct)
    {
        RetrieveCallCount++;
        return Task.FromResult(retrieveReturns);
    }

    public Task RemoveSigninRequestStateAsync(Guid stateId, Ct ct)
    {
        RemoveCallCount++;
        return Task.CompletedTask;
    }

    public Task UpdateSigninRequestStateAsync(Guid stateId, SamlAuthenticationState state, Ct ct)
    {
        UpdateCallCount++;
        LastUpdatedStateId = stateId;
        LastUpdatedState = state;
        return Task.CompletedTask;
    }
}
