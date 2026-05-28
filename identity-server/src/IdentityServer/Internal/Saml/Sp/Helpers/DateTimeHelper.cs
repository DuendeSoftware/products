// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    static class DateTimeHelper
    {
        internal static DateTime? EarliestTime(DateTime? value1, DateTime? value2)
        {
            if (value1 == null ||
                value1.HasValue && value2.HasValue && value1.Value > value2.Value)
            {
                return value2;
            }

            return value1;
        }
    }
}
