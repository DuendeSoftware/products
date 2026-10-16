// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Spaces.Internal.Telemetry;

internal static class SpacesTelemetryConstants
{
    public const string MeterName = "Duende.Spaces";

    public static class Instruments
    {
        public const string DataProtectionDefaultSpaceFallback = "duende.spaces.data_protection.default_space_fallback";
    }

    public static class Tags
    {
        public const string DataProtectionOperation = "duende.spaces.data_protection.operation";
    }

    public static class TagValues
    {
        public const string Unprotect = "unprotect";
        public const string DangerousUnprotect = "dangerous_unprotect";
    }
}
