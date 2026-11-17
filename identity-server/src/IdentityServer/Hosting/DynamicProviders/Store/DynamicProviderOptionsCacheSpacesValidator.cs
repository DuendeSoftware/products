// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.Spaces;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Hosting.DynamicProviders.Store;

/// <summary>
/// Fails <see cref="SpacesOptions"/> validation when a dynamic provider options type is not cached by
/// <see cref="SpaceAwareDynamicOptionsMonitorCache{TOptions}"/>, because its options would then be shared
/// between spaces. Only Spaces resolves <see cref="SpacesOptions"/>, so the host fails only when Spaces is set up.
/// </summary>
#pragma warning disable CA1812 // DynamicProviderOptionsCacheSpacesValidator is instantiated via DI
internal sealed class DynamicProviderOptionsCacheSpacesValidator(
    IServiceProvider services,
    IOptions<IdentityServerOptions> identityServerOptions) : IValidateOptions<SpacesOptions>
#pragma warning restore CA1812
{
    public ValidateOptionsResult Validate(string? name, SpacesOptions options)
    {
        var failures = identityServerOptions.Value.DynamicProviders.ProviderTypes.Values
            .DistinctBy(p => p.OptionsType)
            .Where(p => !p.HasSpaceAwareOptionsCache(services))
            .Select(p => FailureMessage(p.OptionsType.FullName))
            .ToList();

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static string FailureMessage(string? optionsType) =>
        $"The IOptionsMonitorCache<{optionsType}> for this dynamic provider options type is not the space-aware cache " +
        "IdentityServer registers through AddDynamicProvider, so options for a dynamic provider in one space could be used in another. " +
        "This happens when the type was added with AddProviderType instead of AddDynamicProvider, " +
        $"or when the application registered its own IOptionsMonitorCache<{optionsType}>.";
}
