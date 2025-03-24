// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable disable

using Duende.IdentityServer.Configuration;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer;

// APIs needed for IdentityServer specific license validation
internal class IdentityServerLicenseValidator : LicenseValidator<IdentityServerLicense>
{
    internal readonly static IdentityServerLicenseValidator Instance = new IdentityServerLicenseValidator();

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

    private void EnsureAdded(ref HashSet<string> hashSet, object lockObject, string key)
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
    private bool _validateClientWarned = false;

    // Internal method that takes license as parameter to allow testing
    internal void ValidateClient(string clientId, IdentityServerLicense license)
    {
        if (license != null && !license.ClientLimit.HasValue)
        {
            return;
        }

        EnsureAdded(ref _clientIds, _clientIdLock, clientId);

        if (license != null)
        {
            if (_clientIds.Count > license.ClientLimit)
            {
                ErrorLog.Invoke(
                    "Your license for Duende IdentityServer only permits {clientLimit} number of clients. You have processed requests for {clientCount}. The clients used were: {clients}.",
                    [license.ClientLimit, _clientIds.Count, _clientIds.ToArray()]);
            }
        }
        else
        {
            if (!_validateClientWarned && _clientIds.Count > 5)
            {
                _validateClientWarned = true;
                WarningLog?.Invoke(
                    "You do not have a license, and you have processed requests for {clientCount} clients. This number requires a tier of license higher than Starter Edition. The clients used were: {clients}.",
                    [_clientIds.Count, _clientIds.ToArray()]);
            }
        }
    }

    private HashSet<string> _issuers = new();
    private object _issuerLock = new();
    private bool _validateIssuerWarned = false;
    public void ValidateIssuer(string iss)
    {
        if (License != null && !License.IssuerLimit.HasValue)
        {
            return;
        }

        EnsureAdded(ref _issuers, _issuerLock, iss);

        if (License != null)
        {
            if (_issuers.Count > License.IssuerLimit)
            {
                ErrorLog.Invoke(
                    "Your license for Duende IdentityServer only permits {issuerLimit} number of issuers. You have processed requests for {issuerCount}. The issuers used were: {issuers}. This might be due to your server being accessed via different URLs or a direct IP and/or you have reverse proxy or a gateway involved. This suggests a network infrastructure configuration problem, or you are deliberately hosting multiple URLs and require an upgraded license.",
                    [License.IssuerLimit, _issuers.Count, _issuers.ToArray()]);
            }
        }
        else
        {
            if (!_validateIssuerWarned && _issuers.Count > 1)
            {
                _validateIssuerWarned = true;
                WarningLog?.Invoke(
                    "You do not have a license, and you have processed requests for {issuerCount} issuers. If you are deliberately hosting multiple URLs then this number requires a license per issuer, or the Enterprise Edition tier of license. If not then this might be due to your server being accessed via different URLs or a direct IP and/or you have reverse proxy or a gateway involved, and this suggests a network infrastructure configuration problem. The issuers used were: {issuers}.",
                    [_issuers.Count, _issuers.ToArray()]);
            }
        }
    }

    private bool ValidateServerSideSessionsWarned = false;
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

    private bool CanUseDPoPWarned = false;
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

    private bool ValidateResourceIndicatorsWarned = false;
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

    private bool ValidateParWarned = false;
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

    private bool ValidateDynamicProvidersWarned = false;
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

    private bool ValidateCibaWarned = false;
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
