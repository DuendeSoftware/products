// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Common;
using Duende.IdentityServer.Saml.Endpoints.Results;
using Duende.IdentityServer.Saml.Metadata;
using Duende.IdentityServer.Saml.Serialization;
using Duende.IdentityServer.Saml.Xml;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.ResponseHandling;

/// <inheritdoc />
public class Saml2MetadataResponseGenerator(
    TimeProvider timeProvider,
    IOptions<IdentityServerOptions> identityServerOptions,
    ISamlXmlWriter samlXmlWriter)
    : ISaml2MetadataResponseGenerator
{
    private readonly SamlOptions _samlOptions = identityServerOptions.Value.Saml;
    /// <inheritdoc/>
    public async Task<Saml2MetadataResult> GenerateMetadataAsync(string issuer,
        IEnumerable<X509Certificate2> signingKeys, SamlOptions options, string baseUrl, Ct ct)
    {
        var entity = await GenerateEntityDescriptorAsync(issuer, signingKeys, options, baseUrl, ct);

        var xml = samlXmlWriter.Write(entity);

        return new()
        {
            Xml = xml
        };
    }

    /// <summary>
    /// Generate the EntityDescriptor
    /// </summary>
    /// <returns></returns>
    protected virtual Task<EntityDescriptor> GenerateEntityDescriptorAsync(string issuer,
        IEnumerable<X509Certificate2> signingKeys, SamlOptions options, string baseUrl, Ct ct)
    {
        IDPSSODescriptor iDPSSODescriptor = new();

        var validUntil = timeProvider.GetUtcNow() + options.Metadata.ExpiryDuration;

        EntityDescriptor entity = new()
        {
            EntityId = issuer,
            CacheDuration = options.Metadata.CacheDuration,
            ValidUntil = new DateTimeUtc(validUntil.Ticks),
            Id = XmlHelpers.CreateId(),
            RoleDescriptors =
            {
                iDPSSODescriptor
            }
        };

        foreach (var signingKey in signingKeys)
        {
            var keyInfo = new KeyInfo();
            keyInfo.AddClause(new KeyInfoX509Data(signingKey));
            iDPSSODescriptor.Keys.Add(new KeyDescriptor
            {
                KeyInfo = keyInfo,
                Use = KeyUse.Signing
            });
        }

        iDPSSODescriptor.SingleSignOnServices.AddRange(
            options.Endpoints.SingleSignOnServiceBindings.Select(b =>
                new Endpoint
                {
                    Binding = b,
                    Location = baseUrl.TrimEnd('/') + options.Endpoints.SingleSignOnServicePath
                }));

        iDPSSODescriptor.SingleLogoutServices.AddRange(
            options.Endpoints.SingleLogoutServiceBindings.Select(b =>
                new Endpoint
                {
                    Binding = b,
                    Location = baseUrl.TrimEnd('/') + options.Endpoints.SingleLogoutServicePath
                }));

        iDPSSODescriptor.NameIdFormats.AddRange(_samlOptions.SupportedNameIdFormats);
        iDPSSODescriptor.WantAuthnRequestsSigned = _samlOptions.WantAuthnRequestsSigned;

        return Task.FromResult(entity);
    }
}
