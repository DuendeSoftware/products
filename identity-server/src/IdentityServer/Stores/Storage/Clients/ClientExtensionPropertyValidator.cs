// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Admin.Clients;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Stores.Storage.Clients;

internal sealed class ClientExtensionPropertyValidator(ISchemaStore schemaStore) : IConfigurationValidator<ClientConfiguration>
{
    public async Task<IReadOnlyList<StorageError>> ValidateAsync(ClientConfiguration configuration, Ct ct)
    {
        if (configuration.ExtendedProperties.Count == 0)
        {
            return [];
        }

        var schema = await schemaStore.GetAsync(SchemaId.Client, ct);

        var attributes = new AttributeValueCollection();


        foreach (var attribute in configuration.ExtendedProperties)
        {
            attributes.Set(attribute);
        }

        // Todo: we should be able to show which properties have errors.
        if (!attributes.TryValidateAgainst(schema, out var errors))
        {
            return [StorageError.ValidationFailed(string.Join("; ", errors))];
        }

        return [];
    }
}
