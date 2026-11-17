// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.Spaces.Internal;

#pragma warning disable CA1812 // SharedIssuerUriSpacesValidator is instantiated via DI
internal sealed class SharedIssuerUriSpacesValidator(IOptions<IdentityServerOptions> identityServerOptions)
    : IValidateOptions<SpacesOptions>
#pragma warning restore CA1812
{
    public ValidateOptionsResult Validate(string? name, SpacesOptions options) =>
        options.AllowSharedIssuerUri || identityServerOptions.Value.IssuerUri.IsMissing()
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                "IdentityServerOptions.IssuerUri is set, which gives every space the same issuer. " +
                "If spaces also share signing keys, a token issued in one space can be accepted in another space when its other checks, such as the audience and client, also pass there. " +
                "Remove IssuerUri so each space gets its own issuer, or set SpacesOptions.AllowSharedIssuerUri to true to accept this.");
}
