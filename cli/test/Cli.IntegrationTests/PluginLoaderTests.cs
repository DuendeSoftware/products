// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Runtime.Loader;
using Duende.Cli.Plugins;

namespace Duende.Cli.IntegrationTests;

public class PluginLoaderTests
{
    private static readonly string AssemblyPath =
        typeof(Duende.Cli.TestPlugin.TestCliPlugin).Assembly.Location;

    [Fact]
    public void PluginLoadsViaPluginLoader()
    {
        var plugin = PluginLoader.Load(AssemblyPath);

        plugin.Name.ShouldBe("test");
    }

    [Fact]
    public void PluginReturnsCommandWithCorrectName()
    {
        var plugin = PluginLoader.Load(AssemblyPath);

        var command = plugin.GetCommand();

        command.Name.ShouldBe("test");
    }

    [Fact]
    public void PluginCommandHasSubcommand()
    {
        var plugin = PluginLoader.Load(AssemblyPath);

        var command = plugin.GetCommand();

        command.Subcommands.ShouldContain(c => c.Name == "hello");
    }

    [Fact]
    public void PluginLoadsInIsolatedAlc()
    {
        var plugin = PluginLoader.Load(AssemblyPath);

        var context = AssemblyLoadContext.GetLoadContext(plugin.GetType().Assembly);

        context.ShouldNotBe(AssemblyLoadContext.Default);
    }
}
