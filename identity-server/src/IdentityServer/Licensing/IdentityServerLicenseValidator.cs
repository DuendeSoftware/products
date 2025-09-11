// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable disable

using Duende.IdentityServer.Configuration;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer;

// APIs needed for IdentityServer specific license validation
internal class IdentityServerLicenseValidator : LicenseValidator<IdentityServerLicense>
{
    internal readonly static IdentityServerLicenseValidator Instance = new();

    private IdentityServerOptions _options;

    public void Initialize(ILoggerFactory loggerFactory, IdentityServerOptions options, bool isDevelopment = false)
    {
        _options = options;

        Initialize(loggerFactory, "IdentityServer", options.LicenseKey);

        if (License?.RedistributionFeature == true && !isDevelopment)
        {
            // for redistribution/prod scenarios, we want most of these to be at trace level
            ErrorLog = WarningLog = InformationLog = DebugLog = LogToTrace;
        }

        ValidateLicense();
    }

    protected override void ValidateExpiration(List<string> errors)
    {
        if (!License.RedistributionFeature)
        {
            base.ValidateExpiration(errors);
        }
    }

    protected override void ValidateProductFeatures(List<string> errors)
    {
        if (License.IsCommunityEdition && License.RedistributionFeature)
        {
            throw new Exception("Invalid License: Redistribution is not valid for the IdentityServer Community Edition.");
        }

        if (License.IsBffEdition)
        {
            throw new Exception("Invalid License: The BFF edition license is not valid for IdentityServer.");
        }

        if (_options.KeyManagement.Enabled && !License.KeyManagementFeature)
        {
            errors.Add("You have automatic key management enabled, but your license does not include that feature of Duende IdentityServer. This feature requires the Business or Enterprise Edition tier of license. Either upgrade your license or disable automatic key management by setting the KeyManagement.Enabled property to false on the IdentityServerOptions.");
        }
    }
    protected override void WarnForProductFeaturesWhenMissingLicense()
    {
        if (_options.KeyManagement.Enabled)
        {
            WarningLog?.Invoke("You have automatic key management enabled, but you do not have a license. This feature requires the Business or Enterprise Edition tier of license. Alternatively you can disable automatic key management by setting the KeyManagement.Enabled property to false on the IdentityServerOptions.", null);
        }
    }

    private static void EnsureAdded(ref HashSet<string> hashSet, object lockObject, string key)
    {
        // Lock free test first.
        if (!hashSet.Contains(key))
        {
            lock (lockObject)
            {
                // Check again after lock, to quite early if another thread
                // already did the job.
                if (!hashSet.Contains(key))
                {
                    // The HashSet is not thread safe. And we don't want to lock for every single
                    // time we use it. Our access pattern should be a lot of reads and a few writes
                    // so better to create a new copy every time we need to add a value.
                    var newSet = new HashSet<string>(hashSet)
                    {
                        key
                    };

                    // Reference assignment is atomic so non-locked readers will handle this.
                    hashSet = newSet;
                }
            }
        }
    }

    public void ValidateClient(string clientId) => ValidateClient(clientId, License);

    private HashSet<string> _clientIds = new();
    private object _clientIdLock = new();

    // Internal method that takes license as parameter to allow testing
    internal void ValidateClient(string clientId, IdentityServerLicense license)
    {
        if (license != null && !license.ClientLimit.HasValue)
        {
            return;
        }

        EnsureAdded(ref _clientIds, _clientIdLock, clientId);

        // Only log for redistribution case because license v2 logs all other cases
        if (license != null && license.RedistributionFeature)
        {
            if (_clientIds.Count > license.ClientLimit)
            {
                ErrorLog.Invoke(
                    "Your license for IdentityServer includes {clientLimit} clients but you have processed requests for {clientCount} clients. Please contact {contactInfo} at {companyName} or start a conversation with us at https://duende.link/l/contact to upgrade your license as soon as possible. In a future version, this limit will be enforced after a threshold is exceeded. The clients used were: {clients}.",
                    [license.ClientLimit, _clientIds.Count, license.ContactInfo, license.CompanyName, _clientIds.ToArray()]);
            }
        }
    }

    private HashSet<string> _issuers = new();
    private object _issuerLock = new();

    public void ValidateIssuer(string iss) => ValidateIssuer(iss, License);

    //Internal method that takes a license as parameter to allow testing
    internal void ValidateIssuer(string iss, IdentityServerLicense license)
    {
        if (License != null && !license.IssuerLimit.HasValue)
        {
            return;
        }

        EnsureAdded(ref _issuers, _issuerLock, iss);

        // Only log for redistribution case because license v2 logs all other cases
        if (license != null && license.RedistributionFeature)
        {
            if (_issuers.Count > license.IssuerLimit)
            {
                ErrorLog.Invoke(
                    "Your license for IdentityServer includes {issuerLimit} issuers but you have processed requests for {issuerCount} issuers. This indicates that requests for each issuer are being sent to this instance of IdentityServer, which may be due to a network infrastructure configuration issue. If you intend to use multiple issuers, please contact {contactInfo} at {companyName} or start a conversation with us at https://duende.link/l/contact to upgrade your license as soon as possible. In a future version, this limit will be enforced after a threshold is exceeded. The issuers used were {issuers}.",
                    [license.IssuerLimit, _issuers.Count, _issuers.ToArray()]);
            }
        }
    }

    private bool ValidateServerSideSessionsWarned;
    public void ValidateServerSideSessions()
    {
        if (License != null)
        {
            if (!License.ServerSideSessionsFeature)
            {
                throw new Exception("You have configured server-side sessions. Your license for Duende IdentityServer does not include that feature. This feature requires the Business or Enterprise Edition tier of license.");
            }
        }
        else if (!ValidateServerSideSessionsWarned)
        {
            ValidateServerSideSessionsWarned = true;
            WarningLog?.Invoke("You have configured server-side sessions, but you do not have a license. This feature requires the Business or Enterprise Edition tier of license.", null);
        }
    }

    private bool CanUseDPoPWarned;
    public void ValidateDPoP()
    {
        if (License != null)
        {
            if (!License.DPoPFeature)
            {
                throw new Exception("A request was made using DPoP. Your license for Duende IdentityServer does not include the DPoP feature. This feature requires the Enterprise Edition tier of license.");
            }
        }
        else if (!CanUseDPoPWarned)
        {
            CanUseDPoPWarned = true;
            WarningLog?.Invoke("A request was made using DPoP, but you do not have a license. This feature requires the Enterprise Edition tier of license.", null);
        }
    }

    private bool ValidateResourceIndicatorsWarned;
    public void ValidateResourceIndicators(string resourceIndicator)
    {
        if (!string.IsNullOrWhiteSpace(resourceIndicator))
        {
            if (License != null)
            {
                if (!License.ResourceIsolationFeature)
                {
                    throw new Exception("A request was made using a resource indicator. Your license for Duende IdentityServer does not permit resource isolation. This feature requires the Enterprise Edition tier of license.");
                }
            }
            else if (!ValidateResourceIndicatorsWarned)
            {
                ValidateResourceIndicatorsWarned = true;
                WarningLog?.Invoke("A request was made using a resource indicator, but you do not have a license. This feature requires the Enterprise Edition tier of license.", Array.Empty<object>());
            }
        }
    }

    private bool ValidateParWarned;
    public void ValidatePar()
    {
        if (License != null)
        {
            if (!License.ParFeature)
            {
                throw new Exception("A request was made to the pushed authorization endpoint. Your license of Duende IdentityServer does not permit pushed authorization. This features requires the Business Edition or higher tier of license.");
            }
        }
        else if (!ValidateParWarned)
        {
            ValidateParWarned = true;
            WarningLog?.Invoke("A request was made to the pushed authorization endpoint, but you do not have a license. This feature requires the Business Edition or higher tier of license.", Array.Empty<object>());
        }
    }

    public void ValidateResourceIndicators(IEnumerable<string> resourceIndicators)
    {
        if (resourceIndicators?.Any() == true)
        {
            if (License != null)
            {
                if (!License.ResourceIsolationFeature)
                {
                    throw new Exception("A request was made using a resource indicator. Your license for Duende IdentityServer does not permit resource isolation. This feature requires the Enterprise Edition tier of license.");
                }
            }
            else if (!ValidateResourceIndicatorsWarned)
            {
                ValidateResourceIndicatorsWarned = true;
                WarningLog?.Invoke("A request was made using a resource indicator, but you do not have a license. This feature requires the Enterprise Edition tier of license.", Array.Empty<object>());
            }
        }
    }

    private bool ValidateDynamicProvidersWarned;
    public void ValidateDynamicProviders()
    {
        if (License != null)
        {
            if (!License.DynamicProvidersFeature)
            {
                throw new Exception("A request was made invoking a dynamic provider. Your license for Duende IdentityServer does not permit dynamic providers. This feature requires the Enterprise Edition tier of license.");
            }
        }
        else if (!ValidateDynamicProvidersWarned)
        {
            ValidateDynamicProvidersWarned = true;
            WarningLog?.Invoke("A request was made invoking a dynamic provider, but you do not have a license. This feature requires the Enterprise Edition tier of license.", null);
        }
    }

    private bool ValidateCibaWarned;
    public void ValidateCiba()
    {
        if (License != null)
        {
            if (!License.CibaFeature)
            {
                throw new Exception("A CIBA (client initiated backchannel authentication) request was made. Your license for Duende IdentityServer does not permit the CIBA feature. This feature requires the Enterprise Edition tier of license.");
            }
        }
        else if (!ValidateCibaWarned)
        {
            ValidateCibaWarned = true;
            WarningLog?.Invoke("A CIBA (client initiated backchannel authentication) request was made, but you do not have a license. This feature requires the Enterprise Edition tier of license.", null);
        }
    }
}
