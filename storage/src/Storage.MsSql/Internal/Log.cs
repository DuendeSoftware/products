// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Microsoft.Extensions.Logging;

namespace Duende.Storage.MsSql.Internal;

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = $"Checking schema version")]
    internal static partial void CheckingSchemaVersion(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = $"Creating schema {{{Parameters.SchemaName}}}")]
    internal static partial void CreatingSchema(ILogger logger, string schemaName);

    [LoggerMessage(Level = LogLevel.Information, Message = $"Migrating schema {{{Parameters.SchemaName}}}")]
    internal static partial void MigratingSchema(ILogger logger, string schemaName);

    [LoggerMessage(Level = LogLevel.Information, Message = $"Verifying schema {{{Parameters.SchemaName}}}")]
    internal static partial void VerifyingSchema(ILogger logger, string schemaName);

    [LoggerMessage(Level = LogLevel.Information, Message = $"Executing migration step V{{{Parameters.FromVersion}}}→V{{{Parameters.ToVersion}}}")]
    internal static partial void ExecutingMigrationStep(ILogger logger, int fromVersion, int toVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = $"Error While creating schema")]
    internal static partial void ErrorWhileCreatingSchema(ILogger logger, Exception e);

    [LoggerMessage(Level = LogLevel.Debug, Message = $"Executing sql {{{Parameters.Sql}}}")]
    internal static partial void ExecutingSql(ILogger logger, string sql);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = $"Creating DSO: {nameof(Parameters.EntityType)}={{{Parameters.EntityType}}}, {nameof(Parameters.Id)}={{{Parameters.Id}}}, {nameof(Parameters.DsoSchemaVersion)}={{{Parameters.DsoSchemaVersion}}}")]
    internal static partial void CreatingDso(ILogger logger, EntityType entityType, Guid id, uint dsoSchemaVersion);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = $"Deleting DSO: {nameof(Parameters.EntityType)}={{{Parameters.EntityType}}}, {nameof(Parameters.Id)}={{{Parameters.Id}}}")]
    internal static partial void DeletingDso(ILogger logger, EntityType entityType, Guid id);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = $"Reading DSO: {nameof(Parameters.EntityType)}={{{Parameters.EntityType}}}, {nameof(Parameters.Id)}={{{Parameters.Id}}}")]
    internal static partial void ReadingDso(ILogger logger, EntityType entityType, Guid id);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = $"Reading DSOs: {nameof(Parameters.EntityType)}={{{Parameters.EntityType}}}, {nameof(Parameters.Count)}={{{Parameters.Count}}}")]
    internal static partial void ReadingDsos(ILogger logger, EntityType entityType, int count);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = $"Updating DSO: {nameof(Parameters.EntityType)}={{{Parameters.EntityType}}}, {nameof(Parameters.Id)}={{{Parameters.Id}}}, {nameof(Parameters.DsoSchemaVersion)}={{{Parameters.DsoSchemaVersion}}}, {nameof(Parameters.ExpectedEntityVersion)}={{{Parameters.ExpectedEntityVersion}}}")]
    internal static partial void UpdatingDso(ILogger logger, EntityType entityType, Guid id, uint dsoSchemaVersion, int expectedEntityVersion);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = $"Querying DSOs: {nameof(Parameters.EntityType)}={{{Parameters.EntityType}}}, {nameof(Parameters.Skip)}={{{Parameters.Skip}}}, {nameof(Parameters.Take)}={{{Parameters.Take}}}")]
    internal static partial void QueryingDsos(ILogger logger, EntityType entityType, int skip, int take);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = $"Querying DSO fields: {nameof(Parameters.EntityType)}={{{Parameters.EntityType}}}, {nameof(Parameters.FieldCount)}={{{Parameters.FieldCount}}}, {nameof(Parameters.Skip)}={{{Parameters.Skip}}}, {nameof(Parameters.Take)}={{{Parameters.Take}}}")]
    internal static partial void QueryingFieldsDsos(ILogger logger, EntityType entityType, int fieldCount, int skip, int take);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = $"Executing query: {nameof(Parameters.Query)}={{{Parameters.Query}}}")]
    internal static partial void ExecutingQuery(ILogger logger, string query);

    private static class Parameters
    {
        internal const string FromVersion = nameof(FromVersion);
        internal const string ToVersion = nameof(ToVersion);
        internal const string Count = nameof(Count);
        internal const string DsoSchemaVersion = nameof(DsoSchemaVersion);
        internal const string EntityType = nameof(EntityType);
        internal const string ExpectedEntityVersion = nameof(ExpectedEntityVersion);
        internal const string FieldCount = nameof(FieldCount);
        internal const string Id = nameof(Id);
        internal const string Skip = nameof(Skip);
        internal const string Take = nameof(Take);
        internal const string Query = nameof(Query);
        internal const string SchemaName = nameof(SchemaName);
        internal const string Sql = nameof(Sql);
    }


}
