// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml.Endpoints.Results;

namespace Duende.IdentityServer.Saml.ResponseHandling;

/// <summary>
/// Saml2 metadata response generator
/// </summary>
public interface ISaml2MetadataResponseGenerator
{
    /// <summary>
    /// Generates metadata response.
    /// </summary>
    /// <param name="issuer">Entity Id of IdentityServer</param>
    /// <param name="signingKeys"></param>
    /// <param name="options">Saml options</param>
    /// <param name="baseUrl">Base url of IdentityServer</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<Saml2MetadataResult> GenerateMetadataAsync(string issuer, IEnumerable<X509Certificate2> signingKeys,
        SamlOptions options, string baseUrl, Ct ct);
}
