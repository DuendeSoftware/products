// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Spaces.Internal.DataProtection;

internal static class SpacesDataProtectionValidation
{
    public static void Validate(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<SpacesOptions>>().Value;
        if (!options.PartitionDataProtection)
        {
            return;
        }

        // Inspect the service descriptors rather than resolving IEnumerable<IDataProtectionProvider>:
        // resolving the enumerable would run the factories of every other registered provider and keep
        // those instances alive, which is a side effect a validation step must not have.
        var state = services.GetService<SpacesDataProtectionRegistrationState>();
        if (state is null)
        {
            throw new InvalidOperationException(
                "Spaces data protection was not registered. " +
                "Call AddSpaces() before building the app, or set SpacesOptions.PartitionDataProtection = false.");
        }

        var descriptors = state.NonKeyedProviderDescriptors();
        if (descriptors.Count == 0 || !ReferenceEquals(descriptors[^1], state.Installed))
        {
            throw new InvalidOperationException(
                "SpacesOptions.PartitionDataProtection is enabled, but IDataProtectionProvider was registered or replaced after AddSpaces(). " +
                "Register the provider before AddSpaces(), or set SpacesOptions.PartitionDataProtection = false.");
        }

        if (descriptors.Count > 1)
        {
            throw new InvalidOperationException(
                "SpacesOptions.PartitionDataProtection is enabled, but another IDataProtectionProvider is registered. " +
                "Register the provider once, before AddSpaces(), or set SpacesOptions.PartitionDataProtection = false.");
        }
    }
}
