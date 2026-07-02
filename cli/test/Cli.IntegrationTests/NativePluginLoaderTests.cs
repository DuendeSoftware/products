// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Runtime.Loader;
using Duende.Cli.Plugins;

namespace Duende.Cli.IntegrationTests;

/// <summary>
/// Verifies that plugins with native (unmanaged) dependencies load and execute
/// correctly in the isolated <see cref="AssemblyLoadContext"/>.
/// The plugin is loaded from its own output directory (not the test's output) to
/// simulate real deployment where the host and plugin live in separate directories.
/// </summary>
public sealed class NativePluginLoaderTests
{
    /// <summary>
    /// Path to the test native plugin assembly in its own build output directory.
    /// Using the plugin's output directory (not the test's) ensures native DLLs are
    /// only resolvable through the ALC's <c>LoadUnmanagedDll</c> override, not via
    /// default probing from the test runner's directory.
    /// </summary>
    private static readonly string PluginAssemblyPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "testing", "Cli.TestNativePlugin", "bin",
#if DEBUG
            "Debug",
#else
            "Release",
#endif
            "net10.0",
            "Duende.Cli.TestNativePlugin.dll"));

    [Fact]
    public async Task Plugin_with_native_dependencies_executes_in_isolated_alc()
    {
        File.Exists(PluginAssemblyPath).ShouldBeTrue($"Plugin not found at {PluginAssemblyPath}. Build the Cli.TestNativePlugin project first.");

        var plugin = PluginLoader.Load(PluginAssemblyPath);
        var context = AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly);

        var result = await plugin.GetCommand().Parse([]).InvokeAsync();

        result.ShouldBe(0);
        _ = context.ShouldNotBeNull();
        context.ShouldNotBe(AssemblyLoadContext.Default);
    }
}
