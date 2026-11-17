// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.Options;

namespace Duende.Spaces.Internal;

#pragma warning disable CA1812 // SpacesOptionsValidator is instantiated via DI
internal sealed class SpacesOptionsValidator : IValidateOptions<SpacesOptions>
#pragma warning restore CA1812
{
    public ValidateOptionsResult Validate(string? name, SpacesOptions options)
    {
        var prefix = options.SpacePathPrefix?.Value;
#pragma warning disable CA1308 // Space paths are only resolved in lowercase
        var lowercasePrefix = prefix?.ToLowerInvariant();
#pragma warning restore CA1308

        return string.Equals(prefix, lowercasePrefix, StringComparison.Ordinal)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"SpacesOptions.SpacePathPrefix '{prefix}' must be lowercase, for example '{lowercasePrefix}'. " +
                "Spaces only resolves lowercase space paths, so a prefix with uppercase letters would never match a request.");
    }
}
