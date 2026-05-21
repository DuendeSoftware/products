// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Cli.Plugins;

namespace Duende.Cli.Tests;

public class PluginPreloaderTests
{
    [Fact]
    public void DetectRequestedPlugin_returns_known_plugin_name()
    {
        var result = PluginPreloader.DetectRequestedPlugin(["storage", "--help"]);

        result.ShouldBe("storage");
    }

    [Fact]
    public void DetectRequestedPlugin_skips_plugin_path_value()
    {
        var result = PluginPreloader.DetectRequestedPlugin(["--plugin-path", "foo.dll", "storage"]);

        result.ShouldBe("storage");
    }

    [Fact]
    public void DetectRequestedPlugin_returns_null_for_non_plugin_command()
    {
        var result = PluginPreloader.DetectRequestedPlugin(["version"]);

        result.ShouldBeNull();
    }

    [Fact]
    public void DetectRequestedPlugin_returns_null_for_option_flag()
    {
        var result = PluginPreloader.DetectRequestedPlugin(["--help"]);

        result.ShouldBeNull();
    }

    [Fact]
    public void DetectRequestedPlugin_returns_null_for_empty_args()
    {
        var result = PluginPreloader.DetectRequestedPlugin([]);

        result.ShouldBeNull();
    }

    [Fact]
    public void DetectPluginPath_returns_path_when_present()
    {
        var result = PluginPreloader.DetectPluginPath(
            ["--plugin-path", "/path/to/plugin.dll", "storage"]);

        result.ShouldBe("/path/to/plugin.dll");
    }

    [Fact]
    public void DetectPluginPath_returns_null_when_not_present()
    {
        var result = PluginPreloader.DetectPluginPath(["storage", "--help"]);

        result.ShouldBeNull();
    }

    [Fact]
    public void DetectVersion_returns_version_when_present()
    {
        var result = PluginPreloader.DetectVersion(["--plugin-version", "7.2.1", "storage"]);

        result.ShouldBe("7.2.1");
    }

    [Fact]
    public void DetectVersion_returns_null_when_not_present()
    {
        var result = PluginPreloader.DetectVersion(["storage", "--help"]);

        result.ShouldBeNull();
    }

    [Fact]
    public void DetectRequestedPlugin_skips_version_flag_value()
    {
        var result = PluginPreloader.DetectRequestedPlugin(["--plugin-version", "7.2.1", "storage"]);

        result.ShouldBe("storage");
    }
}
