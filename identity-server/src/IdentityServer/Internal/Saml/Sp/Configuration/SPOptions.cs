// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;
using Duende.IdentityServer.Internal.Saml.Sp.Tokens;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Sp.Configuration
{
    /// <summary>
    /// Options for the service provider's behaviour; i.e. everything except
    /// the idp and federation list.
    /// </summary>
    internal class SPOptions
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public SPOptions()
        {
            MetadataCacheDuration = new XsdDuration(hours: 1);
            Compatibility = new Compatibility();
            OutboundSigningAlgorithm = XmlHelpers.GetDefaultSigningAlgorithmName();
            MinIncomingSigningAlgorithm = XmlHelpers.GetDefaultSigningAlgorithmName();
        }

        /// <summary>
        /// Return Uri to redirect the client to, if no return uri was specified
        /// when initiating the signin sequence.
        /// </summary>
        public Uri ReturnUrl { get; set; }

        /// <summary>
        /// Recommendation of cache refresh interval to those who reads our
        /// metadata.
        /// </summary>
        public XsdDuration MetadataCacheDuration { get; set; }

        /// <summary>
        /// Maximum validity duration after fetch for those who reads our
        /// metadata. Exposed as an absolute validUntil time in the metadata.
        /// If set to null, no validUntil is exposed in metadata.
        /// </summary>
        public TimeSpan? MetadataValidDuration { get; set; }

        volatile private Saml2PSecurityTokenHandler saml2PSecurityTokenHandler;

        /// <summary>
        /// The security token handler used to process incoming assertions for this SP.
        /// The default value is to lazy create one using the current EntityId.
        /// </summary>
        public Saml2PSecurityTokenHandler Saml2PSecurityTokenHandler
        {
            get
            {
                // Capture in a local variable to prevent race conditions. Reads and writes
                // of references are atomic so there is no need for a lock.
                var value = saml2PSecurityTokenHandler;
                if (value == null)
                {
                    // Set the saved value, but don't trust it - still use a local var for the return.
                    saml2PSecurityTokenHandler = value = new Saml2PSecurityTokenHandler(this);
                }

                return value;
            }
            set
            {
                saml2PSecurityTokenHandler = value;
            }
        }

        /// <summary>
        /// Url to discovery service to use if no idp is specified in the sign in call.
        /// </summary>
        public Uri DiscoveryServiceUrl { get; set; }

        private EntityId entityId;

        /// <summary>
        /// EntityId - The identity of the ServiceProvider to use when sending requests to Idp
        /// and presenting the SP in metadata.
        /// </summary>
        public EntityId EntityId
        {
            get
            {
                return entityId;
            }
            set
            {
                if (saml2PSecurityTokenHandler != null)
                {
                    throw new InvalidOperationException("Can't change entity id when a token handler has been instantiated.");
                }
                entityId = value;
            }
        }

        private string modulePath = "/Saml2";

        /// <summary>
        /// Application root relative path for Saml2 endpoints. The
        /// default is "/Saml2".
        /// </summary>
        public string ModulePath
        {
            get
            {
                return modulePath;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                value = value.TrimEnd('/');

                if (!value.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    value = "/" + value;
                }

                modulePath = value;
            }
        }

        /// <summary>
        /// By default, the service provider uses the host, protocol, port and
        /// application root path from the HTTP request when creating links. 
        /// This might not be accurate in reverse proxy or load-balancing
        /// situations. You can override the origin used for link generation
        /// for the entire application using this property. To override per request,
        /// implement a <code>GetPublicOrigin</code> Notification function.
        /// </summary>
        public Uri PublicOrigin { get; set; }

        /// <summary>
        /// Metadata describing the organization responsible for the entity.
        /// </summary>
        public Organization Organization { get; set; }

        /// <summary>
        /// NameId Policy.
        /// </summary>
        public Saml2NameIdPolicy NameIdPolicy { get; set; }

        /// <summary>
        /// RequestedAuthnContext
        /// </summary>
        public Saml2RequestedAuthnContext RequestedAuthnContext { get; set; }

        readonly ICollection<ContactPerson> contacts = new List<ContactPerson>();

        /// <summary>
        /// Collection of contacts for the SAML2 entity.
        /// </summary>
        public ICollection<ContactPerson> Contacts
        {
            get
            {
                return contacts;
            }
        }

        readonly ICollection<AttributeConsumingService> attributeConsumingServices
            = new List<AttributeConsumingService>();

        /// <summary>
        /// Collection of attribute consuming services for the service provider.
        /// </summary>
        public ICollection<AttributeConsumingService> AttributeConsumingServices
        {
            get
            {
                return attributeConsumingServices;
            }
        }

        readonly ServiceCertificateCollection serviceCertificates = new ServiceCertificateCollection();

        /// <summary>
        /// Certificates used by the service provider for signing or decryption.
        /// </summary>
        public ServiceCertificateCollection ServiceCertificates
        {
            get
            {
                return serviceCertificates;
            }
        }

        /// <summary>
        /// Certificates valid for use in decryption
        /// </summary>
        public ReadOnlyCollection<X509Certificate2> DecryptionServiceCertificates
        {
            get
            {
                var decryptionCertificates = ServiceCertificates
                    .Where(c => c.Use.HasFlag(CertificateUse.Encryption) || c.Use == CertificateUse.Both)
                    .Select(c => c.Certificate);

                return decryptionCertificates.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Certificate for use in signing outbound requests
        /// </summary>
        public X509Certificate2 SigningServiceCertificate
        {
            get
            {
                var signingCertificates = ServiceCertificates
                    .Where(c => c.Status == CertificateStatus.Current)
                    .Where(c => c.Use.HasFlag(CertificateUse.Signing) || c.Use == CertificateUse.Both)
                    .Select(c => c.Certificate);

                return signingCertificates.FirstOrDefault();
            }
        }

        /// <summary>
        /// Certificates to be published in metadata
        /// </summary>
        public ReadOnlyCollection<ServiceCertificate> MetadataCertificates
        {
            get
            {
                var futureEncryptionCertExists = PublishableServiceCertificates
                    .Any(c => c.Status == CertificateStatus.Future && (c.Use == CertificateUse.Encryption || c.Use == CertificateUse.Both));

                var metaDataCertificates = PublishableServiceCertificates
                    .Where(
                        // Signing & "Both" certs always get published because we want Idp's to be aware of upcoming keys
                        c => c.Status == CertificateStatus.Future || c.Use != CertificateUse.Encryption
                        // But current Encryption cert stops getting published immediately when a Future one is added
                        // (of course we still decrypt with the current cert, but that's a different part of the code)
                        || (c.Status == CertificateStatus.Current && c.Use == CertificateUse.Encryption && !futureEncryptionCertExists)
                        || c.MetadataPublishOverride != MetadataPublishOverrideType.None
                    ).ToList();

                var futureBothCertExists = metaDataCertificates
                    .Any(c => c.Status == CertificateStatus.Future && c.Use == CertificateUse.Both);

                foreach (var cert in metaDataCertificates)
                {
                    // Just like we stop publishing Encryption cert immediately when a Future one is added,
                    // in the case of a "Both" cert we should switch the current use to Signing so that Idp's stop sending
                    // us certs encrypted with the old key
                    if (cert.Use == CertificateUse.Both && cert.Status == CertificateStatus.Current && futureBothCertExists)
                    {
                        cert.Use = CertificateUse.Signing;
                    }

                    if (cert.MetadataPublishOverride == MetadataPublishOverrideType.PublishEncryption)
                    {
                        cert.Use = CertificateUse.Encryption;
                    }
                    if (cert.MetadataPublishOverride == MetadataPublishOverrideType.PublishSigning)
                    {
                        cert.Use = CertificateUse.Signing;
                    }
                    if (cert.MetadataPublishOverride == MetadataPublishOverrideType.PublishUnspecified)
                    {
                        cert.Use = CertificateUse.Both;
                    }
                }

                return metaDataCertificates.AsReadOnly();
            }
        }

        private static CertificateUse ConvertUse(CertificateUse certificateUse)
        {
            var use = certificateUse & (CertificateUse.Signing | CertificateUse.Encryption);

            if (use == (CertificateUse.Signing | CertificateUse.Encryption))
            {
                use = CertificateUse.Both;
            }
            return use;
        }

        private IEnumerable<ServiceCertificate> PublishableServiceCertificates
        {
            get
            {
                return ServiceCertificates
                    .Where(c => c.MetadataPublishOverride != MetadataPublishOverrideType.DoNotPublish
                    && c.Use != CertificateUse.TlsClient) // Certs that are only Tls should not be published.
                    .Select(c => new ServiceCertificate // Finally create new instances and convert use to ignore Tls.
                    {
                        Use = ConvertUse(c.Use),
                        Status = c.Status,
                        MetadataPublishOverride = c.MetadataPublishOverride,
                        Certificate = c.Certificate
                    });
            }
        }

        /// <summary>
        /// Signing behaviour for AuthnRequests.
        /// </summary>
        public SigningBehavior AuthenticateRequestSigningBehavior { get; set; }

        /// <summary>
        /// Signing algorithm for metadata and outbound messages. Can be 
        /// overriden for each <see cref="IdentityProvider"/>.
        /// </summary>
        public string OutboundSigningAlgorithm { get; set; }

        /// <summary>
        /// Metadata flag that we want assertions to be signed.
        /// </summary>
        public bool WantAssertionsSigned { get; set; }

        /// <summary>
        /// Validate certificates when validating signatures? Normally not a
        /// good idea as SAML2 deployments typically exchange certificates
        /// directly and instead of relying on the public certificate
        /// infrastructure.
        /// </summary>
        public bool ValidateCertificates { get; set; }

        /// <summary>
        /// Compatibility settings. Can be used to make Saml2 accept
        /// certain non-standard behaviour.
        /// </summary>
        public Compatibility Compatibility { get; set; }

        private string minIncomingSigningAlgorithm;

        /// <summary>
        /// Minimum accepted signature algorithm for any incoming messages.
        /// </summary>
        public string MinIncomingSigningAlgorithm
        {
            get
            {
                return minIncomingSigningAlgorithm;
            }
            set
            {
                if (!XmlHelpers.KnownSigningAlgorithms.Contains(value))
                {
                    throw new ArgumentException("The signing algorithm " + value +
                        " is unknown or not supported by the current .NET Framework.");
                }
                minIncomingSigningAlgorithm = value;
            }
        }

        /// <summary>
        /// Adapter to logging framework of hosting application.
        /// </summary>
        public ILoggerAdapter Logger { get; set; }

        private ITokenReplayCache tokenReplayCache;
        public ITokenReplayCache TokenReplayCache
        {
            get
            {
                if (tokenReplayCache == null)
                {
                    tokenReplayCache = new TokenReplayCache();
                }
                return tokenReplayCache;
            }
            set
            {
                tokenReplayCache = value;
            }
        }

        /// <summary>
        /// Template for token validation parameters. Some security critical validation
        /// parameters are set for each use. The Unsafe.TokenValidationParametersCreated notification
        /// is called after those are set if those need to be overriden.
        /// </summary>
        public TokenValidationParameters TokenValidationParametersTemplate { get; } =
            new TokenValidationParameters
            {
                AuthenticationType = "Federation",
                RequireSignedTokens = false,
                ValidateIssuer = false
            }.SetRequireAudience(false); // RequireAudience must be set to false for versions 5.5.0+ that have it.
    }
}
