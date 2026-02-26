// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;

namespace UnitTests.Common;

public class MockConsentService : IConsentService
{
    public bool RequiresConsentResult { get; set; }

    public Task<bool> RequiresConsentAsync(ClaimsPrincipal subject, Client client, IEnumerable<ParsedScopeValue> parsedScopes, Ct _) => Task.FromResult(RequiresConsentResult);

    public ClaimsPrincipal ConsentSubject { get; set; }
    public Client ConsentClient { get; set; }
    public IEnumerable<string> ConsentScopes { get; set; }

    public Task UpdateConsentAsync(ClaimsPrincipal subject, Client client, IEnumerable<ParsedScopeValue> parsedScopes, Ct _)
    {
        ConsentSubject = subject;
        ConsentClient = client;
        ConsentScopes = parsedScopes?.Select(x => x.RawValue);

        return Task.CompletedTask;
    }
}
