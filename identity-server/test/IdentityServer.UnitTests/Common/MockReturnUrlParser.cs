// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class MockReturnUrlParser : ReturnUrlParser
{
    public AuthorizationRequest AuthorizationRequestResult { get; set; }
    public bool IsValidReturnUrlResult { get; set; }

    public MockReturnUrlParser() : base(Enumerable.Empty<IReturnUrlParser>())
    {
    }

    public override Task<AuthorizationRequest> ParseAsync(string returnUrl, CT ct) => Task.FromResult(AuthorizationRequestResult);

    public override bool IsValidReturnUrl(string returnUrl) => IsValidReturnUrlResult;
}
