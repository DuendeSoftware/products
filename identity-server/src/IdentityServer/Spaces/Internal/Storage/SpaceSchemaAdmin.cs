// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.Models;

namespace Duende.Spaces.Internal.Storage;

/// <summary>
/// Keeps the Space schema in the management space. An admin that reads the Space schema from any active space
/// gets the one copy that Spaces reads when it validates extended properties, and a write of it from any space
/// other than the management space fails. Every other schema stays in the active space.
/// </summary>
internal sealed class SpaceSchemaAdmin(Decorator<ISchemaAdmin> inner, ISpaceContextAccessor spaceContextAccessor) : ISchemaAdmin
{
    public async Task<SaveResult<SchemaId>> CreateAsync(SchemaConfiguration schema, Ct ct)
    {
        if (schema.SchemaId != SchemaId.Space)
        {
            return await inner.Instance.CreateAsync(schema, ct);
        }

        if (SpaceOtherThanManagementIsActive)
        {
            return SpaceSchemaIsSharedFailure;
        }

        using (spaceContextAccessor.SetSpace(SpaceId.Management))
        {
            return await inner.Instance.CreateAsync(schema, ct);
        }
    }

    public async Task<GetResult<SchemaConfiguration>> GetAsync(SchemaId schemaId, Ct ct)
    {
        if (schemaId != SchemaId.Space)
        {
            return await inner.Instance.GetAsync(schemaId, ct);
        }

        using (spaceContextAccessor.SetSpace(SpaceId.Management))
        {
            return await inner.Instance.GetAsync(schemaId, ct);
        }
    }

    public async Task<SaveResult<SchemaId>> UpdateAsync(SchemaId schemaId, SchemaConfiguration schema, DataVersion expectedVersion, Ct ct)
    {
        if (schemaId != SchemaId.Space)
        {
            return await inner.Instance.UpdateAsync(schemaId, schema, expectedVersion, ct);
        }

        if (SpaceOtherThanManagementIsActive)
        {
            return SpaceSchemaIsSharedFailure;
        }

        using (spaceContextAccessor.SetSpace(SpaceId.Management))
        {
            return await inner.Instance.UpdateAsync(schemaId, schema, expectedVersion, ct);
        }
    }

    public async Task<SaveResult<SchemaId>> DeleteAsync(SchemaId schemaId, Ct ct)
    {
        if (schemaId != SchemaId.Space)
        {
            return await inner.Instance.DeleteAsync(schemaId, ct);
        }

        if (SpaceOtherThanManagementIsActive)
        {
            return SpaceSchemaIsSharedFailure;
        }

        using (spaceContextAccessor.SetSpace(SpaceId.Management))
        {
            return await inner.Instance.DeleteAsync(schemaId, ct);
        }
    }

    public Task<QueryResult<SchemaSummary>> QueryAsync(Ct ct) => inner.Instance.QueryAsync(ct);

    private bool SpaceOtherThanManagementIsActive =>
        spaceContextAccessor.IsSpaceIdConfigured() && spaceContextAccessor.GetSpaceId() != SpaceId.Management;

    private static SaveResult<SchemaId> SpaceSchemaIsSharedFailure { get; } = Duende.IdentityServer.Models.SaveResult.Failure<SchemaId>(StorageError.ValidationFailed(
        "The Space schema is shared by all spaces. Create, update or delete it with no space set or in the management space.", "SchemaId"));
}
