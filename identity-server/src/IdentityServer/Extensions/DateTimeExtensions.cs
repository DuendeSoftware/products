// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics;

namespace Duende.IdentityServer.Extensions;

internal static class DateTimeExtensions
{
    extension(DateTime creationTime)
    {
        [DebuggerStepThrough]
        public bool HasExceeded(int seconds, DateTime now) => (now > creationTime.AddSeconds(seconds));

        [DebuggerStepThrough]
        public int GetLifetimeInSeconds(DateTime now) => ((int)(now - creationTime).TotalSeconds);

        [DebuggerStepThrough]
        public bool HasExpired(DateTime now)
        {
            if (now > creationTime)
            {
                return true;
            }

            return false;
        }
    }

    extension(DateTime? expirationTime)
    {
        [DebuggerStepThrough]
        public bool HasExpired(DateTime now)
        {
            if (expirationTime.HasValue &&
                expirationTime.Value.HasExpired(now))
            {
                return true;
            }

            return false;
        }
    }
}
