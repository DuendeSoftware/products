// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Xml.Linq;
using Duende.IdentityServer.Internal.Saml.Sp.Bindings;
using Duende.IdentityServer.Internal.Saml.Sp.Commands;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Duende.IdentityServer.Internal.Saml.Sp.Protocol;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Represents a known identity provider that this service provider can communicate with.
    /// </summary>
    internal class IdentityProvider
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="entityId">Entity id of the identityprovider.</param>
        /// <param name="spOptions">Service provider options to use when 
        /// creating AuthnRequests for this Idp.</param>
        /// <param name="timeProvider">Time provider to use for time-based operations.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "sp")]
        public IdentityProvider(EntityId entityId, SPOptions spOptions, TimeProvider timeProvider)
        {
            EntityId = entityId;

            this.spOptions = spOptions ?? throw new ArgumentNullException(nameof(spOptions));

            OutboundSigningAlgorithm = spOptions.OutboundSigningAlgorithm;
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        readonly SPOptions spOptions;
        private readonly TimeProvider _timeProvider;

        private void Validate()
        {
            if (Binding == 0)
            {
                throw new InvalidOperationException("Missing binding configuration on Idp " + EntityId.Id + ".");
            }

            if (!SigningKeys.Any())
            {
                throw new InvalidOperationException("Missing signing certificate configuration on Idp " + EntityId.Id + ".");
            }

            if (SingleSignOnServiceUrl == null)
            {
                throw new InvalidOperationException("Missing assertion consumer service url configuration on Idp " + EntityId.Id + ".");
            }
        }

        private bool loadMetadata;

        /// <summary>
        /// Should this idp load metadata? The metadata is loaded immediately
        /// when the property is set to true, so the <see cref="MetadataLocation"/>
        /// must be correct before settingLoadMetadata to true.</summary>
        public bool LoadMetadata
        {
            get
            {
                return loadMetadata;
            }
            set
            {
                loadMetadata = value;
                try
                {
                    DoLoadMetadata();
                    Validate();
                }
                catch (WebException)
                {
                    // Ignore if metadata load failed, an automatic
                    // retry has been scheduled.
                }
            }
        }

        private Saml2BindingType binding;

        /// <summary>
        /// The binding used when sending AuthnRequests to the identity provider.
        /// </summary>
        public Saml2BindingType Binding
        {
            get
            {
                ReloadMetadataIfRequired();
                return binding;
            }
            set
            {
                binding = value;
            }
        }

        private Uri singleSignOnServiceUrl;

        /// <summary>
        /// The Url of the single sign on service. This is where the browser is redirected or
        /// where the post data is sent to when sending an AuthnRequest to the idp.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "SignOn")]
        public Uri SingleSignOnServiceUrl
        {
            get
            {
                ReloadMetadataIfRequired();
                return singleSignOnServiceUrl;
            }
            set
            {
                singleSignOnServiceUrl = value;
            }
        }



        Uri singleLogoutServiceUrl;
        /// <summary>
        /// The Url of the single sign out service. This is where the browser
        /// is redirected or where the post data is sent to when sending a
        /// LogoutRequest to the idp.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public Uri SingleLogoutServiceUrl
        {
            get
            {
                ReloadMetadataIfRequired();
                return singleLogoutServiceUrl;
            }
            set
            {
                singleLogoutServiceUrl = value;
            }
        }

        Uri singleLogoutServiceResponseUrl;
        /// <summary>
        /// The Url to send single logout responses to. Defaults to 
        /// SingleLogoutServiceUrl.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public Uri SingleLogoutServiceResponseUrl
        {
            get
            {
                ReloadMetadataIfRequired();
                return singleLogoutServiceResponseUrl ?? SingleLogoutServiceUrl;
            }
            set
            {
                singleLogoutServiceResponseUrl = value;
            }
        }

        private Saml2BindingType singleLogoutServiceBinding;
        /// <summary>
        /// Binding for the Single logout service. If not set, returns the
        /// same as the main binding (used for AuthnRequests)
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public Saml2BindingType SingleLogoutServiceBinding
        {
            get
            {
                ReloadMetadataIfRequired();
                return singleLogoutServiceBinding == 0
                    ? Binding
                    : singleLogoutServiceBinding;
            }
            set
            {
                singleLogoutServiceBinding = value;
            }
        }

        /// <summary>
        /// The Entity Id of the identity provider.
        /// </summary>
        public EntityId EntityId { get; private set; }

        /// <summary>
        /// Is this idp allowed to send unsolicited responses, i.e. idp initiated sign in?
        /// </summary>
        public bool AllowUnsolicitedAuthnResponse { get; set; }

        /// <summary>
        /// Does the RelayState contains the return url?, 
        /// This setting is used only when the AllowUnsolicitedAuthnResponse setting is enabled.
        /// </summary>
        public bool RelayStateUsedAsReturnUrl { get; set; }

        private string metadataLocation;

        /// <summary>
        /// Location of metadata for the Identity Provider. Automatically enables
        /// <see cref="LoadMetadata"/>. The location can be a URL, an absolute
        /// path to a local file or an app relative  path 
        /// (e.g. ~/App_Data/IdpMetadata.xml). By default the entity id is
        /// interpreted as the metadata location (which is a convention).
        /// </summary>
        public string MetadataLocation
        {
            get
            {
                return metadataLocation ?? EntityId.Id;
            }
            set
            {
                metadataLocation = value;
                LoadMetadata = true;
            }
        }

        /// <summary>
        /// Create an authenticate request aimed for this idp.
        /// </summary>
        /// <param name="saml2Urls">Urls for Saml2, used to populate fields
        /// in the created AuthnRequest</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "AuthenticateRequestSigningBehavior")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "ServiceCertificates")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "AuthenticateRequests")]
        public Saml2AuthenticationRequest CreateAuthenticateRequest(
            Saml2Urls saml2Urls)
        {
            if (saml2Urls == null)
            {
                throw new ArgumentNullException(nameof(saml2Urls));
            }

            var authnRequest = new Saml2AuthenticationRequest()
            {
                DestinationUrl = SingleSignOnServiceUrl,
                AssertionConsumerServiceUrl = saml2Urls.AssertionConsumerServiceUrl,
                Issuer = spOptions.EntityId,
                // For now we only support one attribute consuming service.
                AttributeConsumingServiceIndex = spOptions.AttributeConsumingServices.Any() ? 0 : (int?)null,
                NameIdPolicy = spOptions.NameIdPolicy,
                RequestedAuthnContext = spOptions.RequestedAuthnContext,
                SigningAlgorithm = OutboundSigningAlgorithm
            };

            if (spOptions.AuthenticateRequestSigningBehavior == SigningBehavior.Always
                || (spOptions.AuthenticateRequestSigningBehavior == SigningBehavior.IfIdpWantAuthnRequestsSigned
                && WantAuthnRequestsSigned))
            {
                if (spOptions.SigningServiceCertificate == null)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Idp \"{0}\" is configured for signed AuthenticateRequests, but ServiceCertificates configuration contains no certificate with usage \"Signing\" or \"Both\". To resolve this issue you can a) add a service certificate with usage \"Signing\" or \"Both\" (default if not specified is \"Both\") or b) Set the AuthenticateRequestSigningBehavior configuration property to \"Never\".",
                            EntityId.Id));
                }

                authnRequest.SigningCertificate = spOptions.SigningServiceCertificate;
            }

            return authnRequest;
        }

        /// <summary>
        /// Signing Algorithm to be used when signing oubound messages.
        /// </summary>
        public string OutboundSigningAlgorithm { get; set; }

        /// <summary>
        /// Bind a Saml2 message using the active binding of the idp,
        /// producing a CommandResult with the result of the binding.
        /// </summary>
        /// <remarks>
        /// This overload does not support the usage of Xml Created notifications.
        /// </remarks>
        /// <param name="request">The Saml2 message to bind.</param>
        /// <returns>CommandResult with the bound request.</returns>
        public CommandResult Bind(ISaml2Message request)
        {
            return Saml2Binding.Get(Binding).Bind(request);
        }

        /// <summary>
        /// Bind a Saml2 message using the active binding of hte idp,
        /// producing a CommandResult with the result of the binding.
        /// </summary>
        /// <typeparam name="TMessage">Type of the message.</typeparam>
        /// <param name="message">The Saml2 message to bind.</param>
        /// <param name="xmlCreatedNotification">Notification to call with Xml structure</param>
        /// <returns>CommandResult with the bound message.</returns>
        public CommandResult Bind<TMessage>(
            TMessage message, Action<TMessage, XDocument, Saml2BindingType> xmlCreatedNotification)
            where TMessage : ISaml2Message
        {
            return Saml2Binding.Get(Binding).Bind(message, spOptions.Logger, xmlCreatedNotification);
        }

        private readonly ConfiguredAndLoadedSigningKeysCollection signingKeys =
            new ConfiguredAndLoadedSigningKeysCollection();

        /// <summary>
        /// The public key of the idp that is used to verify signatures of responses/assertions.
        /// </summary>
        public ConfiguredAndLoadedSigningKeysCollection SigningKeys
        {
            get
            {
                ReloadMetadataIfRequired();
                return signingKeys;
            }
        }

        readonly object metadataLoadLock = new object();

        private void DoLoadMetadata()
        {
            if (LoadMetadata)
            {
                lock (metadataLoadLock)
                {
                    try
                    {
                        spOptions.Logger?.WriteInformation("Loading metadata for idp " + EntityId.Id);
                        var metadata = MetadataLoader.LoadIdp(
                            MetadataLocation,
                            spOptions.Compatibility.UnpackEntitiesDescriptorInIdentityProviderMetadata);

                        ReadMetadata(metadata);
                    }
                    catch (WebException ex)
                    {
                        spOptions.Logger?.WriteError("Failed to load metadata for idp " + EntityId.Id, ex);
                        MetadataValidUntil = DateTime.MinValue;
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Reads the supplied metadata and sets all properties of the 
        /// IdentityProvider based on the metadata.
        /// </summary>
        /// <param name="metadata">Metadata to read.</param>
        public void ReadMetadata(EntityDescriptor metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            lock (metadataLoadLock)
            {
                if (metadata.EntityId.Id != EntityId.Id)
                {
                    var msg = string.Format(CultureInfo.InvariantCulture,
                        "Unexpected entity id \"{0}\" found when loading metadata for \"{1}\".",
                        metadata.EntityId.Id, EntityId.Id);
                    throw new InvalidOperationException(msg);
                }

                ReadMetadataIdpDescriptor(metadata);

                MetadataValidUntil = metadata.CalculateMetadataValidUntil(_timeProvider);
            }
        }

        private void ReadMetadataIdpDescriptor(EntityDescriptor metadata)
        {
            var idpDescriptor = metadata.RoleDescriptors
                .OfType<IdpSsoDescriptor>().Single();

            WantAuthnRequestsSigned = idpDescriptor.WantAuthnRequestsSigned ?? false;

            var ssoService = GetPreferredEndpoint(idpDescriptor.SingleSignOnServices);
            if (ssoService != null)
            {
                binding = Saml2Binding.UriToSaml2BindingType(ssoService.Binding);
                singleSignOnServiceUrl = ssoService.Location;
            }

            var sloService = GetPreferredEndpoint(idpDescriptor.SingleLogoutServices);
            if (sloService != null)
            {
                SingleLogoutServiceUrl = sloService.Location;
                SingleLogoutServiceBinding = Saml2Binding.UriToSaml2BindingType(sloService.Binding);
                singleLogoutServiceResponseUrl = sloService.ResponseLocation;
            }

            var keys = idpDescriptor.Keys.Where(k => k.Use == KeyType.Unspecified || k.Use == KeyType.Signing);

            signingKeys.SetLoadedItems(keys.Select(k => k.KeyInfo
                .MakeSecurityKeyIdentifier().First(c => c.CanCreateKey)).ToList());
        }

        private static T GetPreferredEndpoint<T>(ICollection<T> endpoints) where T : Endpoint
        {
            // Prefer an endpoint with a redirect binding, then check for POST which 
            // is the other supported by Saml2.
            return endpoints.FirstOrDefault(s => s.Binding == Saml2Binding.HttpRedirectUri) ??
                endpoints.FirstOrDefault(s => s.Binding == Saml2Binding.HttpPostUri);
        }

        private DateTime? metadataValidUntil;

        /// <summary>
        /// Validity time of the metadata this idp was configured from. Null if
        /// idp was not configured from metadata.
        /// </summary>
        public DateTime? MetadataValidUntil
        {
            get
            {
                return metadataValidUntil;
            }
            private set
            {
                metadataValidUntil = value;

                if (LoadMetadata)
                {
                    ScheduleMetadataRefresh();
                }
            }
        }

        private void ScheduleMetadataRefresh()
        {
            // Use a weak reference to allow garbage collector to collect any
            // non-referenced IdentityProvider objects without the timer being
            // the thing that keeps it alive.
            var weakThis = new WeakReference<IdentityProvider>(this);

            Task.Delay(MetadataRefreshScheduler.GetDelay(MetadataValidUntil.Value, _timeProvider))
                .ContinueWith((_) => DoLoadMetadataIfTargetAlive(weakThis));
        }

        // Exclude because we don't want to wait for a GC run during unit test run
        // to trigger the case when the Idp has been garbaged collected.
        [ExcludeFromCodeCoverage]
        private static void DoLoadMetadataIfTargetAlive(WeakReference<IdentityProvider> target)
        {
            if (target.TryGetTarget(out IdentityProvider idp))
            {
                idp.DoLoadMetadata();
            }
        }

        /// <summary>
        /// Does this Idp want the AuthnRequests signed?
        /// </summary>
        public bool WantAuthnRequestsSigned { get; set; }

        private void ReloadMetadataIfRequired()
        {
            if (LoadMetadata && MetadataValidUntil.Value < _timeProvider.GetUtcNow().UtcDateTime)
            {
                lock (metadataLoadLock)
                {
                    // check the metadata is still expired now we've waited
                    // for the lock to be free - it was probably locked by
                    // another thread performing the refresh
                    if (LoadMetadata && MetadataValidUntil.Value < _timeProvider.GetUtcNow().UtcDateTime)
                    {
                        DoLoadMetadata();
                    }
                }
            }
        }

        /// <summary>
        /// Create a logout request to the idp, for the current identity.
        /// </summary>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "serviceCertificates")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "ServiceCertificates")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "ISPOptions")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public Saml2LogoutRequest CreateLogoutRequest(ClaimsPrincipal user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (spOptions.SigningServiceCertificate == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                    "Tried to issue single logout request to {0}, but no signing certificate for the SP is configured and single logout requires signing. Add a certificate to the ISPOptions.ServiceCertificates collection, or to <serviceCertificates> element if you're using web.config.",
                    EntityId.Id));
            }

            return new Saml2LogoutRequest()
            {
                DestinationUrl = SingleLogoutServiceUrl,
                Issuer = spOptions.EntityId,
                NameId = user.FindFirst(Saml2ClaimTypes.LogoutNameIdentifier)
                            .ToSaml2NameIdentifier(),
                SessionIndex =
                    user.FindFirst(Saml2ClaimTypes.SessionIndex).Value,
                SigningCertificate = spOptions.SigningServiceCertificate,
                SigningAlgorithm = OutboundSigningAlgorithm
            };
        }

        /// <summary>
        /// Disable outbound logout requests to this idp, even though
        /// Saml2 is configured for single logout and the idp supports
        /// it. This setting might be usable when adding SLO to an existing
        /// setup, to ensure that everyone is ready for SLO before activating.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "Logout")]
        public bool DisableOutboundLogoutRequests { get; set; }
    }
}
