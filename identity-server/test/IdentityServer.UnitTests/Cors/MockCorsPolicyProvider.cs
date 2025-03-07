// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace UnitTests.Cors;

public class MockCorsPolicyProvider : ICorsPolicyProvider
{
    public bool WasCalled { get; set; }
    public CorsPolicy Response { get; set; }

    public Task<CorsPolicy> GetPolicyAsync(HttpContext context, string policyName)
    {
        WasCalled = true;
        return Task.FromResult(Response);
    }
}