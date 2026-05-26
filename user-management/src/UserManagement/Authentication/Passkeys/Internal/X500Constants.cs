// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Constants for X.500 Distinguished Name attribute type OIDs.
/// Reference: https://www.alvestrand.no/objectid/2.5.4.html
/// </summary>
/// <remarks>
/// .NET does not expose public constants for these OIDs — the runtime's
/// internal <c>WellKnownOids</c> class is not part of the public API.
/// Reference: https://github.com/dotnet/runtime/issues/87270
/// </remarks>
internal static class X500Constants
{
    /// <summary>
    /// X.500 attribute type OIDs (2.5.4.*).
    /// </summary>
    public static class AttributeTypes
    {
        /// <summary>Common Name (CN) — OID 2.5.4.3</summary>
        public const string CommonName = "2.5.4.3";

        /// <summary>Country (C) — OID 2.5.4.6</summary>
        public const string Country = "2.5.4.6";

        /// <summary>Organization (O) — OID 2.5.4.10</summary>
        public const string Organization = "2.5.4.10";

        /// <summary>Organizational Unit (OU) — OID 2.5.4.11</summary>
        public const string OrganizationalUnit = "2.5.4.11";
    }
}
