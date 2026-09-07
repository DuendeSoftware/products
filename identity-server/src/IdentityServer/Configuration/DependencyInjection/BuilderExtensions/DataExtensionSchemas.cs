// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores.Storage.IdentityProviders;
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
        ///     Registers a fixed set of data extension schemas using an in-memory store.
        ///     Schemas are immutable at runtime. <see cref="ISchemaAdmin"/> is NOT registered;
        ///     attempting to resolve it will fail.
        ///     <para>
        ///         Built-in identity provider schemas (e.g. <c>idp:oidc</c>) are always included
        ///         alongside the caller-supplied schemas so that standard providers continue to work.
        ///     </para>
        /// </summary>
        /// <param name="schemas">The schema definitions to make available.</param>
        /// <returns>The builder for chaining.</returns>
        public IIdentityServerBuilder AddInMemoryDataExtensionSchemas(
            IEnumerable<SchemaConfiguration> schemas)
        {
            // Built-ins come first so that caller-supplied schemas with the same SchemaId override
            // them (InMemorySchemaStore uses last-wins semantics for duplicate IDs).
            var allSchemas = BuiltInSchemas.All.Concat(schemas);
            var store = new InMemorySchemaStore(allSchemas);
            builder.Services.AddTransient<ISchemaAdmin>(_
                => throw new NotSupportedException(
                    $"{nameof(ISchemaAdmin)} is not supported when using In Memory Schemas"));
            builder.Services.AddSingleton<ISchemaStore>(store);
            return builder;
        }
    }
}
