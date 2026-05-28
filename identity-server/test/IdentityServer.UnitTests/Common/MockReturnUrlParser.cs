// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class MockReturnUrlParser : ReturnUrlParser
{
    public IAuthenticationContext? ParseResult { get; set; }
    public bool IsValidReturnUrlResult { get; set; }

    // Convenience setter for tests that use AuthorizationRequest specifically
    public AuthorizationRequest? AuthorizationRequestResult
    {
        get => ParseResult as AuthorizationRequest;
        set => ParseResult = value;
    }

    public MockReturnUrlParser() : base(Enumerable.Empty<IReturnUrlParser>())
    {
    }

    public override Task<IAuthenticationContext?> ParseAsync(string returnUrl, Ct _) => Task.FromResult(ParseResult);

    public override bool IsValidReturnUrl(string returnUrl) => IsValidReturnUrlResult;
}
