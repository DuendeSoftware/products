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
    internal static RsaSecurityKey CreateRsaSecurityKey(this KeyManagementOptions options) => CryptoHelper.CreateRsaSecurityKey(options.RsaKeySize);

    internal static bool IsRetired(this KeyManagementOptions options, TimeSpan age) => (age >= options.KeyRetirementAge);

    internal static bool IsExpired(this KeyManagementOptions options, TimeSpan age) => (age >= options.RotationInterval);

    internal static bool IsWithinInitializationDuration(this KeyManagementOptions options, TimeSpan age) => (age <= options.InitializationDuration);

    internal static TimeSpan GetAge(this IClock clock, DateTime date)
    {
        var now = clock.UtcNow.UtcDateTime;
        if (date > now) now = date;
        return now.Subtract(date);
    }
}
