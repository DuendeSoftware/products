// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Resolves the authorization policy names to use for SCIM reads and writes, given the
/// options a host may have set. A specific name (<see cref="ScimOAuthOptions.ReadAuthorizationPolicyName"/>
/// or <see cref="ScimOAuthOptions.WriteAuthorizationPolicyName"/>) wins over the shared
/// <see cref="ScimOAuthOptions.AuthorizationPolicyName"/>; anything not named falls back to the
/// built-in <c>DuendeScimRead</c>/<c>DuendeScimWrite</c> policies.
/// </summary>
internal static class ScimPolicyNames
{
    public static string ResolveRead(ScimOAuthOptions options) =>
        FirstNonBlank(options.ReadAuthorizationPolicyName, options.AuthorizationPolicyName)
        ?? ScimConstants.ReadPolicyName;

    public static string ResolveWrite(ScimOAuthOptions options) =>
        FirstNonBlank(options.WriteAuthorizationPolicyName, options.AuthorizationPolicyName)
        ?? ScimConstants.WritePolicyName;

    /// <summary>
    /// True when the read policy resolves to a custom policy rather than the built-in <c>DuendeScimRead</c>.
    /// Explicitly naming one of the built-in policy names (ASP.NET Core policy names are matched
    /// case-insensitively) does not count as custom.
    /// </summary>
    public static bool IsReadCustom(ScimOAuthOptions options) =>
        IsCustom(FirstNonBlank(options.ReadAuthorizationPolicyName, options.AuthorizationPolicyName));

    /// <summary>
    /// True when the write policy resolves to a custom policy rather than the built-in <c>DuendeScimWrite</c>.
    /// Explicitly naming one of the built-in policy names (ASP.NET Core policy names are matched
    /// case-insensitively) does not count as custom.
    /// </summary>
    public static bool IsWriteCustom(ScimOAuthOptions options) =>
        IsCustom(FirstNonBlank(options.WriteAuthorizationPolicyName, options.AuthorizationPolicyName));

    private static bool IsCustom(string? resolvedName) =>
        resolvedName is not null
        && !string.Equals(resolvedName, ScimConstants.ReadPolicyName, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(resolvedName, ScimConstants.WritePolicyName, StringComparison.OrdinalIgnoreCase);

    private static string? FirstNonBlank(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        if (!string.IsNullOrWhiteSpace(second))
        {
            return second;
        }

        return null;
    }
}
