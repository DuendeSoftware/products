// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Xml;

namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// Helper methods for DateTime formatting.
    /// </summary>
    internal static class DateTimeExtensions
    {
        extension(DateTime dateTime)
        {
            /// <summary>
            /// Format a datetime for inclusion in SAML messages.
            /// </summary>
            /// <returns>Formatted value.</returns>
            public string ToSaml2DateTimeString()
            {
                return XmlConvert.ToString(dateTime.AddTicks(-(dateTime.Ticks % TimeSpan.TicksPerSecond)),
                    XmlDateTimeSerializationMode.Utc);
            }
        }
    }
}
