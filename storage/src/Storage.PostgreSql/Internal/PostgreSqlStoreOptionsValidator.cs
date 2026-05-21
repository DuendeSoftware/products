// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.Storage.PostgreSql.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
public sealed class PostgreSqlStoreOptionsValidator(string? name) : DataAnnotationValidateOptions<PostgreSqlStoreOptions>(name);
