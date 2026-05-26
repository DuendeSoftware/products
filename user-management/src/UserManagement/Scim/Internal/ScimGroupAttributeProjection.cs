// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Scim.Internal.Endpoints.Groups;

namespace Duende.UserManagement.Scim.Internal;

internal static class ScimGroupAttributeProjection
{
    private static readonly HashSet<string> AlwaysReturned =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ScimConstants.Attributes.Id,
            ScimConstants.Attributes.Schemas,
            ScimConstants.Attributes.Meta
        };

    /// <summary>
    /// Applies attribute projection to a SCIM group resource.
    /// If attributes is non-empty, only return those + always-returned.
    /// If excludedAttributes is non-empty, return all except those (cannot exclude always-returned).
    /// They are mutually exclusive per RFC 7644 §3.4.2.5.
    /// </summary>
    internal static ScimGroupResource Apply(
        ScimGroupResource resource,
        IReadOnlySet<string>? attributes,
        IReadOnlySet<string>? excludedAttributes)
    {
        // If neither specified, return as-is
        if ((attributes is null || attributes.Count == 0) &&
            (excludedAttributes is null || excludedAttributes.Count == 0))
        {
            return resource;
        }

        // attributes takes precedence (they're mutually exclusive per spec)
        if (attributes is not null && attributes.Count > 0)
        {
            return ProjectInclude(resource, attributes);
        }

        return ProjectExclude(resource, excludedAttributes!);
    }

    private static ScimGroupResource ProjectInclude(ScimGroupResource resource, IReadOnlySet<string> attributes)
    {
        var includeDisplayName = attributes.Contains(ScimConstants.Attributes.DisplayName);
        var includeMembers = attributes.Contains(ScimConstants.Attributes.Members);

        return resource with
        {
            DisplayName = includeDisplayName || AlwaysReturned.Contains(ScimConstants.Attributes.DisplayName)
                ? resource.DisplayName
                : null,
            Members = includeMembers ? resource.Members : null
        };
    }

    private static ScimGroupResource ProjectExclude(ScimGroupResource resource, IReadOnlySet<string> excludedAttributes)
    {
        var excludeDisplayName = excludedAttributes.Contains(ScimConstants.Attributes.DisplayName)
                                 && !AlwaysReturned.Contains(ScimConstants.Attributes.DisplayName);
        var excludeMembers = excludedAttributes.Contains(ScimConstants.Attributes.Members)
                             && !AlwaysReturned.Contains(ScimConstants.Attributes.Members);

        return resource with
        {
            DisplayName = excludeDisplayName ? null : resource.DisplayName,
            Members = excludeMembers ? null : resource.Members
        };
    }
}
