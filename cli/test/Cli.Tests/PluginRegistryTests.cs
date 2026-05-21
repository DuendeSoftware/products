// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Cli.Plugins;

namespace Duende.Cli.Tests;

public class PluginRegistryTests
{
    [Fact]
    public void KnownPlugins_contains_storage_key() =>
        PluginRegistry.KnownPlugins.ShouldContainKey("storage");

    [Fact]
    public void Storage_entry_has_correct_PackageId() =>
        PluginRegistry.KnownPlugins["storage"].PackageId.ShouldBe("Duende.Storage.CliPlugin");

    [Fact]
    public void Storage_entry_has_correct_AnchorPackage() =>
        PluginRegistry.KnownPlugins["storage"].AnchorPackage.ShouldBe("Duende.Storage");

    [Theory]
    [InlineData("Storage")]
    [InlineData("STORAGE")]
    [InlineData("sToRaGe")]
    public void Lookup_is_case_insensitive(string key) =>
        PluginRegistry.KnownPlugins.ShouldContainKey(key);
}
