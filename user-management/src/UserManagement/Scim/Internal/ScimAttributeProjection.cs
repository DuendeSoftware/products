// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Scim.Internal.Endpoints.Users;

namespace Duende.UserManagement.Scim.Internal;

internal static class ScimAttributeProjection
{
    private static readonly HashSet<string> AlwaysReturned =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ScimConstants.Attributes.Id,
            ScimConstants.Attributes.Schemas,
            ScimConstants.Attributes.Meta
        };

    // Attributes that must NEVER appear in any response
    private static readonly HashSet<string> NeverReturned =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ScimConstants.Attributes.Password
        };

    /// <summary>
    /// Applies attribute projection to a SCIM user resource.
    /// If attributes is non-empty, only return those + always-returned.
    /// If excludedAttributes is non-empty, return all except those (cannot exclude always-returned).
    /// They are mutually exclusive per RFC 7644 §3.4.2.5.
    /// The password attribute is ALWAYS stripped regardless of projection settings.
    /// </summary>
    internal static ScimUserResource Apply(
        ScimUserResource resource,
        IReadOnlySet<string>? attributes,
        IReadOnlySet<string>? excludedAttributes)
    {
        // Always strip never-returned attributes first (password, etc.)
        resource = StripNeverReturned(resource);

        // If neither specified, return as-is (after stripping)
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

    private static ScimUserResource StripNeverReturned(ScimUserResource resource)
    {
        Dictionary<string, object?>? filteredAdditional = null;

        if (resource.AdditionalAttributes is not null)
        {
            filteredAdditional = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in resource.AdditionalAttributes)
            {
                if (!NeverReturned.Contains(kvp.Key))
                {
                    filteredAdditional[kvp.Key] = kvp.Value;
                }
            }
        }

        return resource with { AdditionalAttributes = filteredAdditional };
    }

    private static ScimUserResource ProjectInclude(ScimUserResource resource, IReadOnlySet<string> attributes)
    {
        // Always include required attributes; include requested ones if not in never-returned
        var includeUserName = attributes.Contains(ScimConstants.Attributes.UserName)
                              && !NeverReturned.Contains(ScimConstants.Attributes.UserName);
        var includeExternalId = attributes.Contains(ScimConstants.Attributes.ExternalId)
                                && !NeverReturned.Contains(ScimConstants.Attributes.ExternalId);

        Dictionary<string, object?>? filteredAdditional = null;
        if (resource.AdditionalAttributes is not null)
        {
            filteredAdditional = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in resource.AdditionalAttributes)
            {
                if (attributes.Contains(kvp.Key) && !NeverReturned.Contains(kvp.Key))
                {
                    filteredAdditional[kvp.Key] = kvp.Value;
                }
            }
        }

        return resource with
        {
            UserName = includeUserName ? resource.UserName : null,
            ExternalId = includeExternalId ? resource.ExternalId : null,
            AdditionalAttributes = filteredAdditional
        };
    }

    private static ScimUserResource ProjectExclude(ScimUserResource resource, IReadOnlySet<string> excludedAttributes)
    {
        // Cannot exclude always-returned attributes
        var excludeUserName = excludedAttributes.Contains(ScimConstants.Attributes.UserName)
                              && !AlwaysReturned.Contains(ScimConstants.Attributes.UserName);
        var excludeExternalId = excludedAttributes.Contains(ScimConstants.Attributes.ExternalId)
                                && !AlwaysReturned.Contains(ScimConstants.Attributes.ExternalId);

        Dictionary<string, object?>? filteredAdditional = null;
        if (resource.AdditionalAttributes is not null)
        {
            filteredAdditional = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in resource.AdditionalAttributes)
            {
                if (!excludedAttributes.Contains(kvp.Key) || AlwaysReturned.Contains(kvp.Key))
                {
                    filteredAdditional[kvp.Key] = kvp.Value;
                }
            }
        }

        return resource with
        {
            UserName = excludeUserName ? null : resource.UserName,
            ExternalId = excludeExternalId ? null : resource.ExternalId,
            AdditionalAttributes = filteredAdditional
        };
    }
}
