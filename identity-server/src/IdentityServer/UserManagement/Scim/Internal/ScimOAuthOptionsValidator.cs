// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Spaces;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal;

internal sealed class ScimOAuthOptionsValidator(
    ScimLocalIssuerMarker? localIssuer = null,
    ISpaceContextAccessor? spaces = null) : IValidateOptions<ScimOAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, ScimOAuthOptions options)
    {
        var readIsCustom = ScimPolicyNames.IsReadCustom(options);
        var writeIsCustom = ScimPolicyNames.IsWriteCustom(options);

        if (readIsCustom && writeIsCustom)
        {
            // Both sides are custom: no built-in scheme is ever used, so none of the checks below apply.
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            if (localIssuer is null)
            {
                failures.Add(
                    "ScimOAuthOptions.Authority must be configured when not using a custom AuthorizationPolicyName. " +
                    "Set Authority to the URL of the identity provider that issues SCIM bearer tokens.");
            }
        }
        else if (spaces is not null)
        {
            failures.Add(
                "ScimOAuthOptions.Authority can't be combined with Spaces and the built-in SCIM policies, " +
                "because every space would accept tokens from that one authority. Remove Authority to validate " +
                "tokens per space, or set ReadAuthorizationPolicyName and WriteAuthorizationPolicyName to policies " +
                "that check the space.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add(
                "ScimOAuthOptions.Audience must not be empty. " +
                "Set Audience to the expected audience value for SCIM bearer tokens (default: \"urn:duende:scim\").");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

