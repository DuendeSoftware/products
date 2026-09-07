// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extensions for Key Management
/// </summary>
public static class KeyManagementExtensions
{
    extension(KeyManagementOptions options)
    {
        internal RsaSecurityKey CreateRsaSecurityKey() => CryptoHelper.CreateRsaSecurityKey(options.RsaKeySize);

        internal bool IsRetired(TimeSpan age) => (age >= options.KeyRetirementAge);

        internal bool IsExpired(TimeSpan age) => (age >= options.RotationInterval);

        internal bool IsWithinInitializationDuration(TimeSpan age) => (age <= options.InitializationDuration);
    }

    extension(TimeProvider timeProvider)
    {
        internal TimeSpan GetAge(DateTime date)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (date > now)
            {
                now = date;
            }

            return now.Subtract(date);
        }
    }
}
