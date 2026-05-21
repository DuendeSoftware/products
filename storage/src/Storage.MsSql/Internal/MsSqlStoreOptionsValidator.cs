// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.Storage.MsSql.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
public sealed class MsSqlStoreOptionsValidator(string name) : DataAnnotationValidateOptions<MsSqlStoreOptions>(name);
