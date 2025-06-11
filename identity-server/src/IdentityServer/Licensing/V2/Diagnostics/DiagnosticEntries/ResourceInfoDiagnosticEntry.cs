// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal class ResourceInfoDiagnosticEntry(ResourceLoadedTracker resourceLoadedTracker) : IDiagnosticEntry
{
    public Task WriteAsync(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("Resources");

        var resourceGroups =
            resourceLoadedTracker.Resources.GroupBy(resource => resource.Value.Type, resource => resource.Value);
        foreach (var group in resourceGroups)
        {
            writer.WriteStartArray(group.Key);

            if (group.Key == "ApiResource")
            {
                WriteApiResources(writer, group);
            }
            else
            {
                WriteSimpleResources(writer, group);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();

        return Task.CompletedTask;
    }

    private static void WriteApiResources(Utf8JsonWriter writer, IEnumerable<TrackedResource> apiResources)
    {
        foreach (var resource in apiResources)
        {
            writer.WriteStartObject();
            writer.WriteString("Name", resource.Name);
            writer.WriteBoolean("ResourceIndicatorRequired", resource.ResourceIndicatorRequired.GetValueOrDefault());
            writer.WriteStartArray("SecretTypes");
            foreach (var secretType in resource.SecretTypes ?? [])
            {
                writer.WriteStringValue(secretType);
            }

            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }

    private static void WriteSimpleResources(Utf8JsonWriter writer, IEnumerable<TrackedResource> resources)
    {
        foreach (var resource in resources)
        {
            writer.WriteStringValue(resource.Name);
        }
    }
}
