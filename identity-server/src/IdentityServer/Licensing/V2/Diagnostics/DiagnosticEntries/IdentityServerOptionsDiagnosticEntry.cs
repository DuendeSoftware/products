// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Infrastructure;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class IdentityServerOptionsDiagnosticEntry(IOptions<IdentityServerOptions> options) : IDiagnosticEntry
{
    private static readonly RemovePropertyModifier<IdentityServerOptions> RemoveLicenseKeyModifier = new([
        nameof(IdentityServerOptions.LicenseKey)
    ]);
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { RemoveLicenseKeyModifier.ModifyTypeInfo }
        },
        WriteIndented = false
    };

    public Task WriteAsync(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("IdentityServerOptions");

        JsonSerializer.Serialize(writer, options.Value, _serializerOptions);

        return Task.CompletedTask;
    }
}
