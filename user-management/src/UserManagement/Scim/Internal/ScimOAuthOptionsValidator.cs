// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

#pragma warning disable duende_experimental

namespace Duende.UserManagement.Scim.Internal;

internal sealed class ScimOAuthOptionsValidator : IValidateOptions<ScimOAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, ScimOAuthOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AuthorizationPolicyName))
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            failures.Add(
                "ScimOAuthOptions.Authority must be configured when not using a custom AuthorizationPolicyName. " +
                "Set Authority to the URL of the identity provider that issues SCIM bearer tokens.");
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

#pragma warning restore duende_experimental
