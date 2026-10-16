// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Spaces;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.EntityFramework;

/// <summary>
/// Fails <see cref="SpacesOptions"/> validation, because the Entity Framework stores keep one database
/// for all spaces. Only Spaces resolves <see cref="SpacesOptions"/>, so the host fails only when Spaces is set up.
/// </summary>
#pragma warning disable CA1812 // SpacesNotSupportedValidator is instantiated via DI
internal sealed class SpacesNotSupportedValidator : IValidateOptions<SpacesOptions>
#pragma warning restore CA1812
{
    public ValidateOptionsResult Validate(string? name, SpacesOptions options) =>
        ValidateOptionsResult.Fail(
            "Duende Spaces cannot be used with the Entity Framework stores, because they do not keep data per space. " +
            "Use AddConfigurationStorage() and AddOperationalStorage() instead of the Entity Framework stores.");
}
