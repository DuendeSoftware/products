// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Runtime.Loader;
using Duende.Cli.PluginAbstractions;

namespace Duende.Cli.Plugins;

/// <summary>
/// Loads a plugin assembly from a file path into an isolated <see cref="AssemblyLoadContext"/>
/// and returns the <see cref="ICliPlugin"/> implementation.
/// </summary>
internal static class PluginLoader
{
    internal static ICliPlugin Load(string assemblyPath)
    {
        var context = new PluginAssemblyLoadContext(assemblyPath);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);

        var attribute = assembly.GetCustomAttributes(typeof(CliPluginAttribute), false)
            .OfType<CliPluginAttribute>()
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Assembly '{assemblyPath}' does not have a [CliPlugin] attribute. " +
                "Ensure the plugin assembly declares [assembly: CliPlugin(typeof(YourPlugin))].");

        if (!typeof(ICliPlugin).IsAssignableFrom(attribute.PluginType))
        {
            throw new InvalidOperationException(
                $"Plugin type '{attribute.PluginType.FullName}' does not implement {nameof(ICliPlugin)}. " +
                $"Ensure the type specified in [CliPlugin(typeof(...))] implements {nameof(ICliPlugin)}.");
        }

        ICliPlugin instance;
        try
        {
            instance = (ICliPlugin)(Activator.CreateInstance(attribute.PluginType)
                ?? throw new InvalidOperationException(
                    $"Activator.CreateInstance returned null for '{attribute.PluginType.FullName}'."));
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create an instance of '{attribute.PluginType.FullName}'. " +
                "Ensure the plugin type has a public parameterless constructor.", ex);
        }

        return instance;
    }
}
