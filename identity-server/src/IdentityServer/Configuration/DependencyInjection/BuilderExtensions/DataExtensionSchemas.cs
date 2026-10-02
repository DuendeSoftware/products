// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods for configuring data extension schema services on <see cref="IIdentityServerBuilder"/>.
/// </summary>
public static class DataExtensionSchemaBuilderExtensions
{
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        ///     Registers data extension schemas that are defined in code.
        /// </summary>
        /// <remarks>
        ///     A schema with the same id as a built-in schema (for example <c>BuiltInSchemas.OidcProvider</c>) replaces it,
        ///     whichever order the calls are made in. To add attributes to a built-in instead, pass
        ///     <c>BuiltInSchemas.OidcProvider.Extend(...)</c>. If the same id is registered twice, the later schema wins.
        ///     Has no effect when <c>AddDynamicSchemas()</c> is used.
        /// </remarks>
        /// <param name="schemas">The schema definitions to make available.</param>
        /// <returns>The builder for chaining.</returns>
        public IIdentityServerBuilder AddInMemoryDataExtensionSchemas(
            IEnumerable<SchemaConfiguration> schemas)
        {
            ArgumentNullException.ThrowIfNull(schemas);

            foreach (var schema in schemas)
            {
                builder.Services.AddSingleton(schema);
            }

            return builder;
        }
    }
}
