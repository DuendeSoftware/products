// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.Stores.Storage.Clients;

internal class ClientExtensionPropertyValidator(ISchemaStore schemaStore) : IConfigurationValidator<ClientConfiguration>
{
    public async Task<IReadOnlyList<AdminError>> ValidateAsync(ClientConfiguration configuration, Ct ct)
    {
        if (configuration.ExtendedProperties.Count == 0)
        {
            return [];
        }

        var schema = await schemaStore.GetAsync(SchemaId.Client, ct);
        if (schema is null)
        {
            throw new InvalidOperationException("ExtendedProperties cannot be used: no client schema is configured. " +
                                                "Register a schema via ISchemaStore to enable extended properties.");
        }

        var attributes = new AttributeValueCollection();


        foreach (var attribute in configuration.ExtendedProperties)
        {
            attributes.Set(attribute);
        }

        // Todo: we should be able to show which properties have errors. 
        if (!attributes.TryValidateAgainst(schema, out var errors))
        {
            return [AdminError.ValidationFailed(string.Join("; ", errors))];
        }

        return [];
    }
}
