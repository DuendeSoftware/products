// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Profiles.Internal;

internal static class SchemaFreshnessCheck
{
    /// <summary>
    /// Returns true if the attributes are valid against the current schema, false otherwise.
    /// Logs a warning if the check fails.
    /// </summary>
    internal static bool IsValid(ValidatedAttributeValueCollection attributes, AttributeSchema currentSchema, ILogger logger)
    {
        var isEmpty = attributes.SchemaId.Equals(ValidatedAttributeValueCollection.Empty.SchemaId)
            && attributes.SchemaVersion == ValidatedAttributeValueCollection.Empty.SchemaVersion;

        if (isEmpty)
        {
            var isCurrentEmpty = currentSchema.SchemaId.Equals(AttributeSchema.Empty.SchemaId)
                && currentSchema.Version == AttributeSchema.Empty.Version;

            if (isCurrentEmpty)
            {
                return true;
            }

            var hasRequired = currentSchema.AttributeDefinitions.Values.Any(d => d.IsRequired);
            if (hasRequired)
            {
                logger.EmptyCollectionRejected(currentSchema.SchemaId);
                return false;
            }

            return true;
        }

        if (attributes.SchemaId.Equals(currentSchema.SchemaId) && attributes.SchemaVersion == currentSchema.Version)
        {
            return true;
        }

        logger.SchemaMismatch(attributes.SchemaId, attributes.SchemaVersion, currentSchema.SchemaId, currentSchema.Version);
        return false;
    }
}
