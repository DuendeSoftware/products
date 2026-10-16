// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

internal static partial class AttributeSchemaRepositoryLog
{
    [LoggerMessage(
        EventName = nameof(SchemaUpconversionNotSaved),
        Level = LogLevel.Warning,
        Message = "Schema {SchemaId} was saved by an earlier version and was upconverted when read, but the upconverted schema wasn't saved ({Result}). The next read tries again.")]
    internal static partial void SchemaUpconversionNotSaved(this ILogger logger, string schemaId, string result);

    [LoggerMessage(
        EventName = nameof(SchemaUpconversionFailed),
        Level = LogLevel.Warning,
        Message = "Schema {SchemaId} was saved by an earlier version and was upconverted when read, but saving the upconverted schema failed. The next read tries again.")]
    internal static partial void SchemaUpconversionFailed(this ILogger logger, Exception exception, string schemaId);
}
