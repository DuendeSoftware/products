// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;

namespace UnitTests.Cors;

public class MockCorsPolicyService : ICorsPolicyService
{
    public bool WasCalled { get; set; }
    public bool Response { get; set; }

    public Task<bool> IsOriginAllowedAsync(string origin, Ct _)
    {
        WasCalled = true;
        return Task.FromResult(Response);
    }
}
