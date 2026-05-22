// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Telemetry;

/// <summary>
/// Constants for storage telemetry tag keys, values, and instrument names.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class StorageTelemetryConstants
{
    /// <summary>The meter name for storage telemetry.</summary>
    public const string MeterName = "Duende.Storage";

    /// <summary>Instrument names for storage metrics.</summary>
    public static class Instruments
    {
        /// <summary>Counter for storage operation count.</summary>
        public const string OperationCount = "duende.storage.operation.count";

        /// <summary>Histogram for storage operation duration.</summary>
        public const string OperationDuration = "duende.storage.operation.duration";
    }

    /// <summary>Tag keys for storage telemetry.</summary>
    public static class Tags
    {
        /// <summary>The operation being performed.</summary>
        public const string Operation = "duende.storage.operation";

        /// <summary>The database system.</summary>
        public const string DbSystem = "db.system";

        /// <summary>The entity type.</summary>
        public const string EntityType = "duende.storage.entity_type";

        /// <summary>The operation result.</summary>
        public const string Result = "duende.storage.result";

        /// <summary>The error type.</summary>
        public const string ErrorType = "error.type";
    }

    /// <summary>Tag values for storage telemetry.</summary>
    public static class TagValues
    {
        /// <summary>Successful operation.</summary>
        public const string Success = "success";

        /// <summary>Failed operation.</summary>
        public const string Error = "error";

        /// <summary>Unknown result.</summary>
        public const string Unknown = "unknown";
    }

    /// <summary>Database provider identifiers.</summary>
    public static class DatabaseProviders
    {
        /// <summary>Microsoft SQL Server.</summary>
        public const string MsSql = "mssql";

        /// <summary>PostgreSQL.</summary>
        public const string PostgreSql = "postgresql";

        /// <summary>SQLite.</summary>
        public const string Sqlite = "sqlite";

        /// <summary>In-memory store.</summary>
        public const string InMemory = "in_memory";
    }

    /// <summary>Operation name constants.</summary>
#pragma warning disable CA1724 // Type name conflicts with namespace
    public static class Operations
#pragma warning restore CA1724
    {
        /// <summary>Create operation.</summary>
        public const string Create = "create";

        /// <summary>Read operation.</summary>
        public const string Read = "read";

        /// <summary>Read many operation.</summary>
        public const string ReadMany = "read_many";

        /// <summary>Update operation.</summary>
        public const string Update = "update";

        /// <summary>Delete operation.</summary>
        public const string Delete = "delete";

        /// <summary>Link operation.</summary>
        public const string Link = "link";

        /// <summary>Unlink operation.</summary>
        public const string Unlink = "unlink";

        /// <summary>Purge expired entities operation.</summary>
        public const string PurgeExpired = "purge_expired";

        /// <summary>Batch operation.</summary>
        public const string Batch = "batch";

        /// <summary>Get outbox events operation.</summary>
        public const string OutboxGet = "outbox_get";

        /// <summary>Delete outbox events operation.</summary>
        public const string OutboxDelete = "outbox_delete";

        /// <summary>Query operation.</summary>
        public const string Query = "query";

        /// <summary>Query fields operation.</summary>
        public const string QueryFields = "query_fields";

        /// <summary>Query links operation.</summary>
        public const string QueryLinks = "query_links";

        /// <summary>Count operation.</summary>
        public const string Count = "count";
    }
}
