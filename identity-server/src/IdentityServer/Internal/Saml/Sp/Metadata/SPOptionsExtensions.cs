// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Duende.IdentityServer.Internal.Saml.Sp.Bindings;
using Duende.IdentityServer.Internal.Saml.Sp.Commands;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;

namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    static class SPOptionsExtensions
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        public static EntityDescriptor CreateMetadata(this SPOptions spOptions, Saml2Urls urls, TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);
            var ed = new EntityDescriptor
            {
                EntityId = spOptions.EntityId,
                Organization = spOptions.Organization,
                CacheDuration = spOptions.MetadataCacheDuration,
            };

            if (spOptions.MetadataValidDuration.HasValue)
            {
                ed.ValidUntil = timeProvider.GetUtcNow().UtcDateTime.Add(spOptions.MetadataValidDuration.Value);
            }

            foreach (var contact in spOptions.Contacts)
            {
                ed.Contacts.Add(contact);
            }

            var spsso = new SpSsoDescriptor()
            {
                WantAssertionsSigned = spOptions.WantAssertionsSigned,
                AuthnRequestsSigned = spOptions.AuthenticateRequestSigningBehavior == SigningBehavior.Always
            };

            spsso.ProtocolsSupported.Add(new Uri("urn:oasis:names:tc:SAML:2.0:protocol"));

            spsso.AssertionConsumerServices.Add(0, new AssertionConsumerService()
            {
                Index = 0,
                IsDefault = true,
                Binding = Saml2Binding.HttpPostUri,
                Location = urls.AssertionConsumerServiceUrl
            });

            foreach (var attributeService in spOptions.AttributeConsumingServices)
            {
                spsso.AttributeConsumingServices.Add(attributeService.Index, attributeService);
            }

            if (spOptions.ServiceCertificates != null)
            {
                var publishCertificates = spOptions.MetadataCertificates;
                foreach (var serviceCert in publishCertificates)
                {
                    var x509Data = new X509Data();
                    x509Data.Certificates.Add(serviceCert.Certificate);
                    var keyInfo = new DSigKeyInfo();
                    keyInfo.Data.Add(x509Data);

                    spsso.Keys.Add(
                        new KeyDescriptor
                        {
                            Use = (KeyType)(byte)serviceCert.Use,
                            KeyInfo = keyInfo
                        }
                    );
                }
            }

            if (spOptions.SigningServiceCertificate != null)
            {
                spsso.SingleLogoutServices.Add(new SingleLogoutService(
                    Saml2Binding.HttpRedirectUri, urls.LogoutUrl));

                if (spOptions.Compatibility.EnableLogoutOverPost)
                {
                    spsso.SingleLogoutServices.Add(new SingleLogoutService(
                        Saml2Binding.HttpPostUri, urls.LogoutUrl));
                }
            }

            if (spOptions.DiscoveryServiceUrl != null
                && !string.IsNullOrEmpty(spOptions.DiscoveryServiceUrl.OriginalString))
            {
                spsso.DiscoveryResponses.Add(0, new DiscoveryResponse
                {
                    Binding = Saml2Binding.DiscoveryResponseUri,
                    Index = 0,
                    IsDefault = true,
                    Location = urls.SignInUrl
                });
            }

            ed.RoleDescriptors.Add(spsso);

            return ed;
        }
    }
}
