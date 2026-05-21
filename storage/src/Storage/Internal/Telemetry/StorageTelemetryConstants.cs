// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Telemetry;

/// <summary>
/// Constants for storage telemetry tag keys, values, and instrument names.
/// </summary>
public static class StorageTelemetryConstants
{
    public const string MeterName = "Duende.Storage";

    public static class Instruments
    {
        public const string OperationCount = "duende.storage.operation.count";
        public const string OperationDuration = "duende.storage.operation.duration";
    }

    public static class Tags
    {
        public const string Operation = "duende.storage.operation";
        public const string DbSystem = "db.system";
        public const string EntityType = "duende.storage.entity_type";
        public const string Result = "duende.storage.result";
        public const string ErrorType = "error.type";
    }

    public static class TagValues
    {
        public const string Success = "success";
        public const string Error = "error";
        public const string Unknown = "unknown";
    }

    public static class DatabaseProviders
    {
        public const string MsSql = "mssql";
        public const string PostgreSql = "postgresql";
        public const string Sqlite = "sqlite";
        public const string InMemory = "in_memory";
    }

#pragma warning disable CA1724 // Type name conflicts with namespace
    public static class Operations
#pragma warning restore CA1724
    {
        public const string Create = "create";
        public const string Read = "read";
        public const string ReadMany = "read_many";
        public const string Update = "update";
        public const string Delete = "delete";
        public const string Link = "link";
        public const string Unlink = "unlink";
        public const string PurgeExpired = "purge_expired";
        public const string Batch = "batch";
        public const string OutboxGet = "outbox_get";
        public const string OutboxDelete = "outbox_delete";
        public const string Query = "query";
        public const string QueryFields = "query_fields";
        public const string QueryLinks = "query_links";
        public const string Count = "count";
    }
}
