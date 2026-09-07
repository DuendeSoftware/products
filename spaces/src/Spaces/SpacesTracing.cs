// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Spaces;

/// <summary>
/// Constants for Spaces telemetry.
/// </summary>
public static class SpacesTracing
{
    /// <summary>
    /// The ActivitySource name used by Spaces middleware.
    /// Register this with OpenTelemetry via <c>.AddSource(SpacesTracing.ActivitySourceName)</c>
    /// to capture space-scoped traces.
    /// </summary>
    public const string ActivitySourceName = "Duende.Spaces";
}
