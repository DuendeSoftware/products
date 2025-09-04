// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.IntegrationTests.Common;

internal class MockResourceValidator : IResourceValidator
{
    public ResourceValidationResult Result { get; set; } = new ResourceValidationResult();

    public Task<IEnumerable<ParsedScopeValue>> ParseRequestedScopesAsync(IEnumerable<string> scopeValues) => Task.FromResult(scopeValues.Select(x => new ParsedScopeValue(x)));

    public Task<ResourceValidationResult> ValidateRequestedResourcesAsync(ResourceValidationRequest request) => Task.FromResult(Result);
}
