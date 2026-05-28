// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;
using Duende.IdentityServer.Internal.Saml.Sp.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Represents a federation known to this service provider.
    /// </summary>
    internal class Federation
    {
        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="metadataLocation">Location (url, local path or app 
        /// relative path such as ~/App_Data) where metadata is located.</param>
        /// <param name="allowUnsolicitedAuthnResponse">Should unsolicited responses 
        /// from idps in this federation be accepted?</param>
        /// <param name="options">Options to pass on to created IdentityProvider
        /// instances and register identity providers in.</param>
        /// <param name="timeProvider">Time provider to use for time-based operations.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "sp")]
        public Federation(string metadataLocation, bool allowUnsolicitedAuthnResponse, IOptions options, TimeProvider timeProvider)
            : this(metadataLocation,
                  allowUnsolicitedAuthnResponse,
                  options,
                  (IEnumerable<SecurityKeyIdentifierClause>)null,
                  timeProvider)
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="metadataLocation">Location (url, local path or app 
        /// relative path such as ~/App_Data) where metadata is located.</param>
        /// <param name="allowUnsolicitedAuthnResponse">Should unsolicited responses 
        /// from idps in this federation be accepted?</param>
        /// <param name="options">Options to pass on to created IdentityProvider
        /// instances and register identity providers in.</param>
        /// <param name="signingKeys">List of signing keys to use to validate metadata.</param>
        /// <param name="timeProvider">Time provider to use for time-based operations.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "sp")]
        public Federation(string metadataLocation,
            bool allowUnsolicitedAuthnResponse,
            IOptions options,
            IEnumerable<X509Certificate2> signingKeys,
            TimeProvider timeProvider)
            : this(metadataLocation,
                  allowUnsolicitedAuthnResponse,
                  options,
                  signingKeys.Select(k => new X509RawDataKeyIdentifierClause(k)),
                  timeProvider)
        { }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="metadataLocation">Location (url, local path or app 
        /// relative path such as ~/App_Data) where metadata is located.</param>
        /// <param name="allowUnsolicitedAuthnResponse">Should unsolicited responses 
        /// from idps in this federation be accepted?</param>
        /// <param name="options">Options to pass on to created IdentityProvider
        /// instances and register identity providers in.</param>
        /// <param name="signingKeys">List of signing keys to use to validate metadata.</param>
        /// <param name="timeProvider">Time provider to use for time-based operations.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "sp")]
        public Federation(string metadataLocation,
            bool allowUnsolicitedAuthnResponse,
            IOptions options,
            IEnumerable<SecurityKeyIdentifierClause> signingKeys,
            TimeProvider timeProvider)
        {
            Init(metadataLocation, allowUnsolicitedAuthnResponse, options, signingKeys, timeProvider);
        }

        // Internal to allow checking from tests.
        internal bool allowUnsolicitedAuthnResponse;
        internal string metadataLocation;
        private IOptions options;
        private TimeProvider _timeProvider;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1500:VariableNamesShouldNotMatchFieldNames", MessageId = "metadataLocation")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1500:VariableNamesShouldNotMatchFieldNames", MessageId = "allowUnsolicitedAuthnResponse")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1500:VariableNamesShouldNotMatchFieldNames", MessageId = "options")]
        private void Init(string metadataLocation,
            bool allowUnsolicitedAuthnResponse,
            IOptions options,
            IEnumerable<SecurityKeyIdentifierClause> signingKeys,
            TimeProvider timeProvider)
        {
            this.allowUnsolicitedAuthnResponse = allowUnsolicitedAuthnResponse;
            this.options = options;
            this.metadataLocation = metadataLocation;
            SigningKeys = signingKeys?.ToList();
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

            LoadMetadata();
        }

        private readonly object metadataLoadLock = new object();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want a retry, regardless of exception type")]
        private void LoadMetadata()
        {
            lock (metadataLoadLock)
            {
                try
                {
                    options.SPOptions.Logger?.WriteInformation("Loading metadata for federation from " + metadataLocation);
                    var metadata = MetadataLoader.LoadFederation(
                        metadataLocation,
                        SigningKeys,
                        options.SPOptions.ValidateCertificates,
                        options.SPOptions.MinIncomingSigningAlgorithm);

                    var identityProvidersMetadata = metadata.ChildEntities.Cast<EntityDescriptor>()
                        .Where(ed => ed.RoleDescriptors.OfType<IdpSsoDescriptor>().Any());

                    var identityProviders = new List<IdentityProvider>();

                    foreach (var idpMetadata in identityProvidersMetadata)
                    {
                        var idp = new IdentityProvider(idpMetadata.EntityId, options.SPOptions, _timeProvider)
                        {
                            AllowUnsolicitedAuthnResponse = allowUnsolicitedAuthnResponse
                        };

                        idp.ReadMetadata(idpMetadata);
                        identityProviders.Add(idp);
                    }

                    RegisterIdentityProviders(identityProviders);

                    MetadataValidUntil = metadata.CalculateMetadataValidUntil(_timeProvider);

                    LastMetadataLoadException = null;
                }
                catch (Exception ex)
                {
                    options.SPOptions.Logger?.WriteError("Metadata loading failed from " + metadataLocation, ex);
                    var now = _timeProvider.GetUtcNow().UtcDateTime;

                    if (MetadataValidUntil < now)
                    {
                        // If download failed, ignore the error and trigger a scheduled reload.
                        RemoveAllRegisteredIdentityProviders();
                        MetadataValidUntil = DateTime.MinValue;
                    }
                    else
                    {
                        ScheduleMetadataReload();
                    }

                    LastMetadataLoadException = ex;
                }
            }
        }

        // Used for testing.
        internal Exception LastMetadataLoadException { get; private set; }

        // Use a string and not EntityId as List<> doesn't support setting a
        // custom equality comparer as required to handle EntityId correctly.
        private IDictionary<string, EntityId> registeredIdentityProviders = new Dictionary<string, EntityId>();

        private void RegisterIdentityProviders(List<IdentityProvider> identityProviders)
        {
            // Add or update the idps in the new metadata.
            foreach (var idp in identityProviders)
            {
                options.IdentityProviders[idp.EntityId] = idp;
                registeredIdentityProviders.Remove(idp.EntityId.Id);
            }

            // Remove idps from previous set of metadata that were not updated now.
            foreach (var idp in registeredIdentityProviders.Values)
            {
                options.IdentityProviders.Remove(idp);
            }

            // Remember what we registered this time, to know what to remove nex time.
            registeredIdentityProviders = identityProviders.ToDictionary(
                i => i.EntityId.Id,
                i => i.EntityId);
        }

        private void RemoveAllRegisteredIdentityProviders()
        {
            foreach (var idp in registeredIdentityProviders.Values)
            {
                options.IdentityProviders.Remove(idp);
            }

            registeredIdentityProviders.Clear();
        }

        private DateTime metadataValidUntil;

        /// <summary>
        /// For how long is the metadata that the federation has loaded valid?
        /// Null if there is no limit.
        /// </summary>
        public DateTime MetadataValidUntil
        {
            get
            {
                return metadataValidUntil;
            }
            private set
            {
                metadataValidUntil = value;
                ScheduleMetadataReload();
            }
        }

        /// <summary>
        /// Signing keys to use to verify the metadata before using it.
        /// </summary>
        public IList<SecurityKeyIdentifierClause> SigningKeys { get; private set; }

        private void ScheduleMetadataReload()
        {
            var delay = MetadataRefreshScheduler.GetDelay(metadataValidUntil, _timeProvider);
            Task.Delay(delay).ContinueWith((_) => LoadMetadata());
        }
    }
}
