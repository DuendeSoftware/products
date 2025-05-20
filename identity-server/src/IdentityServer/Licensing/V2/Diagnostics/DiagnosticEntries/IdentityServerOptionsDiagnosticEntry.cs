// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Duende.IdentityServer.Configuration;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class IdentityServerOptionsDiagnosticEntry(IOptions<IdentityServerOptions> options) : IDiagnosticEntry
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { RemoveLicenseKeyModifier }
        },
        WriteIndented = false
    };

    public Task WriteAsync(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("IdentityServerOptions");

        JsonSerializer.Serialize(writer, options.Value, _serializerOptions);

        return Task.CompletedTask;
    }

    private static void RemoveLicenseKeyModifier(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(IdentityServerOptions))
        {
            return;
        }

        var propsToKeep = typeInfo.Properties
            .Where(prop => prop.Name != nameof(IdentityServerOptions.LicenseKey))
            .ToArray();

        typeInfo.Properties.Clear();
        foreach (var prop in propsToKeep)
        {
            typeInfo.Properties.Add(prop);
        }
    }
}
