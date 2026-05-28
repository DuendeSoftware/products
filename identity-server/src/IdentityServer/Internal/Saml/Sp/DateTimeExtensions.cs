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
        /// <summary>
        /// Format a datetime for inclusion in SAML messages.
        /// </summary>
        /// <param name="dateTime">Datetime to format.</param>
        /// <returns>Formatted value.</returns>
        public static string ToSaml2DateTimeString(this DateTime dateTime)
        {
            return XmlConvert.ToString(dateTime.AddTicks(-(dateTime.Ticks % TimeSpan.TicksPerSecond)),
                XmlDateTimeSerializationMode.Utc);
        }
    }
}
