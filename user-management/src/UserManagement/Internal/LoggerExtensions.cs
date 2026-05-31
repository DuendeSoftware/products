// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Internal;

internal static class LoggerExtensions
{
    internal static IDisposable? BeginAttributeScope(this ILogger logger, AttributeCode code, object value)
        => logger.BeginScope(new[]
        {
            new KeyValuePair<string, object?>(LogParameters.AttributeCode, code.Value),
            new KeyValuePair<string, object?>(LogParameters.AttributeValue, value)
        });

    internal static IDisposable? BeginSubjectScope(this ILogger logger, UserSubjectId subjectId)
        => logger.BeginScope(new[] { new KeyValuePair<string, object?>(LogParameters.SubjectId, subjectId.Value) });
}
