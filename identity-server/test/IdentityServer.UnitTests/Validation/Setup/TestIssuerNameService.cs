// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;

namespace UnitTests.Validation.Setup;

internal class TestIssuerNameService : IIssuerNameService
{
    private readonly string _value;

    public TestIssuerNameService(string value = null) => _value = value ?? "https://identityserver";

    public Task<string> GetCurrentAsync(Ct ct) => Task.FromResult(_value);
}
