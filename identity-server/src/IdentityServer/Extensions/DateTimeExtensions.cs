// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics;

namespace Duende.IdentityServer.Extensions;

internal static class DateTimeExtensions
{
    [DebuggerStepThrough]
    public static bool HasExceeded(this DateTime creationTime, int seconds, DateTime now) => now > creationTime.AddSeconds(seconds);

    [DebuggerStepThrough]
    public static int GetLifetimeInSeconds(this DateTime creationTime, DateTime now) => (int)(now - creationTime).TotalSeconds;

    [DebuggerStepThrough]
    public static bool HasExpired(this DateTime? expirationTime, DateTime now)
    {
        if (expirationTime.HasValue &&
            expirationTime.Value.HasExpired(now))
        {
            return true;
        }

        return false;
    }

    [DebuggerStepThrough]
    public static bool HasExpired(this DateTime expirationTime, DateTime now)
    {
        if (now > expirationTime)
        {
            return true;
        }

        return false;
    }
}
