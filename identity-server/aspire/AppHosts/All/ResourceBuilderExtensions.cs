// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace aspire.orchestrator.AppHost;

internal static class ResourceBuilderExtensions
{
    public static IResourceBuilder<ProjectResource> AddIdentityAndApiReferences(this IResourceBuilder<ProjectResource> builder,
        IDictionary<string, IResourceBuilder<ProjectResource>> registry)
    {
        string[] referenceNames = ["is-host", "simple-api", "resource-based-api", "dpop-api", "mtls-api"];

        foreach (var referenceName in referenceNames)
        {
            if (registry.TryGetValue(referenceName, out var reference))
            {
                builder = builder
                    .WithReference(reference)
                    .WithEnvironment(
                        name: referenceName,
                        endpointReference: reference.GetEndpoint(name: "https")
                    );
            }
        }
        return builder;
    }
}
