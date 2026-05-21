// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Cli.PluginAbstractions;

/// <summary>
/// Assembly-level attribute that identifies the type implementing <see cref="ICliPlugin"/>.
/// The CLI host scans loaded plugin assemblies for this attribute to discover plugin entry points.
/// </summary>
/// <example>
/// <code>
/// [assembly: CliPlugin(typeof(StorageCliPlugin))]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
public sealed class CliPluginAttribute : Attribute
{
    /// <summary>
    /// Gets the type that implements <see cref="ICliPlugin"/>.
    /// </summary>
    public Type PluginType { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="CliPluginAttribute"/>.
    /// </summary>
    /// <param name="pluginType">The type implementing <see cref="ICliPlugin"/>.</param>
    public CliPluginAttribute(Type pluginType)
    {
        ArgumentNullException.ThrowIfNull(pluginType);

        if (!typeof(ICliPlugin).IsAssignableFrom(pluginType))
        {
            throw new ArgumentException(
                $"Type '{pluginType.FullName}' does not implement {nameof(ICliPlugin)}.", nameof(pluginType));
        }

        if (pluginType.IsAbstract)
        {
            throw new ArgumentException(
                $"Type '{pluginType.FullName}' is abstract and cannot be used as a plugin.", nameof(pluginType));
        }

        PluginType = pluginType;
    }
}
