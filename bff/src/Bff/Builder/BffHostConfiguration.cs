// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Builder;

internal record BffHostConfiguration
{
    public string? Services { get; set; }

    public bool IsEnabled(BffApplicationPartType partType)
    {
        if (Services == null)
        {
            return true; // default to enabled if no configuration is provided
        }
        var parts = Services.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Contains(partType.ToString(), StringComparer.OrdinalIgnoreCase);
    }

}
