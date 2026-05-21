// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using System.Runtime.Loader;

namespace Duende.Cli.Plugins;

/// <summary>
/// Isolated <see cref="AssemblyLoadContext"/> for a single plugin.
/// Loads plugin assemblies and their dependencies in isolation, while sharing
/// <c>Duende.Cli.PluginAbstractions</c> and <c>System.CommandLine</c> from the default context.
/// </summary>
internal sealed class PluginAssemblyLoadContext(string pluginAssemblyPath) : AssemblyLoadContext(
    name: Path.GetFileNameWithoutExtension(pluginAssemblyPath),
    isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginAssemblyPath);

    // Assemblies shared between host and plugin — must not be loaded twice
    private static readonly HashSet<string> SharedAssemblies =
    [
        "Duende.Cli.PluginAbstractions",
        "Microsoft.Extensions.Logging.Abstractions",
        "System.CommandLine",
    ];

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Delegate shared assemblies to the default context
        if (assemblyName.Name is not null && SharedAssemblies.Contains(assemblyName.Name))
        {
            return null; // null = fall through to default context
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is not null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}
