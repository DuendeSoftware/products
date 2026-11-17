// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
#nullable enable

namespace Duende.IdentityServer.DataExtensions.Internal;

/// <summary>
///     Maps a <see cref="SchemaConfiguration"/> to an <see cref="IReadOnlyAttributeSchema"/>.
/// </summary>
internal static class SchemaConfigurationMapper
{
    /// <summary>
    ///     Converts a <see cref="SchemaConfiguration"/> to a read-only <see cref="IReadOnlyAttributeSchema"/>
    ///     suitable for runtime validation.
    /// </summary>
    internal static IReadOnlyAttributeSchema ToReadOnlySchema(SchemaConfiguration config) =>
        AttributeSchema.Load(config.AttributeDefinitions, config.Groups);
}
