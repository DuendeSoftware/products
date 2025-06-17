// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Sets up a fallback configuration source for the "is-host" setting
/// and settings necessary for API service discovery.
/// </summary>
public class FallbackNonAspireContextConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var isRunningUnderAspire = builder is ConfigurationManager configManager && configManager["IS_ASPIRE"] != null;
        return new FallbackNonAspireContextConfigurationProvider(isRunningUnderAspire);
    }
}

public class FallbackNonAspireContextConfigurationProvider(bool isRunningUnderAspire) : ConfigurationProvider
{
    public override void Load()
    {
        if (isRunningUnderAspire)
        {
            return;
        }

        Data = new Dictionary<string, string?>
        {
            { "is-host", "https://localhost:5001" },
            { "Services:dpop-api:https", "https://localhost:6003"},
            { "Services:mtls-api:https", "https://localhost:6004"},
            { "Services:resource-based-api:https", "https://localhost:6002" },
            { "Services:simple-api:https", "https://localhost:6001" }
        };
    }
}
