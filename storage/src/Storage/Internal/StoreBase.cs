// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Base class for store implementations that provides the space identifier.
/// Stores derive from this to receive the space context at construction time
/// rather than reading it from an ambient ISpaceContextAccessor.
/// </summary>
internal abstract class StoreBase
{
    /// <summary>
    /// The space identifier that scopes all operations performed by this store instance.
    /// </summary>
    protected PoolId PoolId { get; private set; } = -1;

    public void SetPoolId(PoolId poolId) => PoolId = poolId;

    /// <summary>
    /// Creates the exception that an engine throws when its database reports that a Storage table does not exist.
    /// </summary>
    /// <param name="databaseException">The provider exception, kept as the inner exception.</param>
    protected static InvalidOperationException StorageSchemaMissing(Exception databaseException) => new(
        "The database reports that a Duende Storage table does not exist. " +
        "The Storage schema may not have been created yet, or the connection string or SchemaName may point to another database or schema. " +
        "To create the schema, run 'duende storage migrate' or call IStorageInstanceSchema.MigrateAsync.",
        databaseException);
}


