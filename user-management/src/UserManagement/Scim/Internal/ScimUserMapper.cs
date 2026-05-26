// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using UserProfile = Duende.UserManagement.Profiles.Internal.UserProfile;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Maps between domain User entities/DSOs and SCIM User resources.
/// </summary>
internal static class ScimUserMapper
{
    /// <summary>
    /// Maps a <see cref="UserProfile"/> and version to a <see cref="ScimUserResource"/>.
    /// userName is sourced from the domain <see cref="UserName"/> property; externalId and
    /// custom attributes are sourced from profile attributes.
    /// </summary>
    internal static ScimUserResource MapToResource(
        UserProfile profile,
        int version,
        string baseUrl,
        string routePrefix)
    {
        var id = profile.SubjectId.Value;
        var (externalId, userName, additionalAttributes) = ExtractProfileAttributes(profile);

        return new ScimUserResource
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            Id = id,
            ExternalId = externalId,
            UserName = userName,
            Meta = new ScimMeta
            {
                ResourceType = ScimConstants.ResourceTypes.User,
                Location = $"{baseUrl}{routePrefix}/{id}",
                Version = ((ScimETag)version).ToHeaderValue()
            },
            AdditionalAttributes = additionalAttributes.Count > 0 ? additionalAttributes : null
        };
    }

    /// <summary>
    /// Maps a <see cref="UserProfileListItem"/> to a <see cref="ScimUserResource"/> (for list responses, no version/ETag).
    /// userName is sourced from the domain <see cref="UserName"/> property.
    /// </summary>
    internal static ScimUserResource MapToResource(UserProfileListItem item, string baseUrl, string routePrefix)
    {
        var id = item.SubjectId.Value;
        string? externalId = null;
        var userName = item.UserName?.Value;
        var additionalAttributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in item.Attributes)
        {
            if (name.Equals(ScimConstants.ExternalIdAttributeName, StringComparison.OrdinalIgnoreCase))
            {
                externalId = value.ToString();
            }
            else
            {
                additionalAttributes[name] = ConvertAttributeValue(value);
            }
        }

        return new ScimUserResource
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            Id = id,
            ExternalId = externalId,
            UserName = userName,
            Meta = new ScimMeta
            {
                ResourceType = ScimConstants.ResourceTypes.User,
                Location = $"{baseUrl}{routePrefix}/{id}",
                Version = null // version not available for list items
            },
            AdditionalAttributes = additionalAttributes.Count > 0 ? additionalAttributes : null
        };
    }

    private static (string? ExternalId, string? UserName, Dictionary<string, object?> AdditionalAttributes) ExtractProfileAttributes(
        UserProfile profile)
    {
        string? externalId = null;
        var userName = profile.UserName?.Value;
        var additionalAttributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in profile.Attributes.Values)
        {
            if (attribute.Code.Value.Equals(ScimConstants.ExternalIdAttributeName, StringComparison.OrdinalIgnoreCase))
            {
                externalId = attribute.UntypedValue.ToString();
            }
            else
            {
                additionalAttributes[attribute.Code.Value] = ConvertAttributeValue(attribute.UntypedValue);
            }
        }

        return (externalId, userName, additionalAttributes);
    }

    private static object? ConvertAttributeValue(object? value) =>
        value switch
        {
            null => null,
            bool b => b,
            int i => i,
            decimal d => d,
            DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
            string s => s,
            IReadOnlyDictionary<string, object> dict =>
                dict.ToDictionary(kvp => kvp.Key, kvp => ConvertAttributeValue(kvp.Value)),
            IReadOnlyList<object> list =>
                list.Select(ConvertAttributeValue).ToList(),
            _ => value.ToString()
        };
}
