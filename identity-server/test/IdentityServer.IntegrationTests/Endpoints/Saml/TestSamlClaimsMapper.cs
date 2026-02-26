// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Saml;

/// <summary>
/// Test implementation of ISamlClaimsMapper that returns a single custom attribute.
/// Used by both unit and integration tests.
/// </summary>
public class TestSamlClaimsMapper : ISamlClaimsMapper
{
    public Task<IEnumerable<SamlAttribute>> MapClaimsAsync(SamlClaimsMappingContext mappingContext)
    {
        var attributes = new List<SamlAttribute>
        {
            new()
            {
                Name = "CUSTOM_MAPPED",
                NameFormat = "urn:oasis:names:tc:SAML:2.0:attrname-format:basic",
                Values = new List<string> { "custom_value" }
            }
        };
        return Task.FromResult<IEnumerable<SamlAttribute>>(attributes);
    }
}
