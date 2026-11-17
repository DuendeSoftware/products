// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

/// <summary>
/// Interface for database-specific test resource operations.
/// Implementations handle acquiring connection strings and cleaning up schemas for different database providers.
/// </summary>
public interface ITestDatabaseProvider
{
    /// <summary>
    /// Gets the Aspire resource name for this database provider.
    /// </summary>
    string ResourceName { get; }

    /// <summary>
    /// Gets the connection string for NCrunch mode (when Aspire host is not available).
    /// This should return a connection string that connects to a manually started database.
    /// </summary>
    string GetNCrunchConnectionString();

    /// <summary>
    /// Gets the connection string for this database provider.
    /// </summary>
    /// <param name="getConnectionString">Delegate to retrieve the connection string from the Aspire host.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string> GetConnectionStringAsync(Func<string, Ct, Task<string>> getConnectionString, Ct ct);

    /// <summary>
    /// Registers a schema name that was created. When the schema is actually created,
    /// call this method to ensure it gets cleaned up on dispose.
    /// </summary>
    void RegisterSchemaName(string schemaName);

    /// <summary>
    /// Drops all registered schemas. Called during dispose.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task DropAllSchemasAsync(Ct ct);
}
