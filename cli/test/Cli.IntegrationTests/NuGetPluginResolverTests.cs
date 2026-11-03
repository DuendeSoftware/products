// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IO.Compression;
using Duende.Cli.Plugins;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Duende.Cli.IntegrationTests;

public sealed class NuGetPluginResolverTests : IDisposable
{
    private const string TestPluginId = "Duende.Cli.TestPlugin";
    private const string NativePluginId = "Duende.Cli.TestNativePlugin";

    private static readonly string TestPluginAssemblyPath =
        typeof(Duende.Cli.TestPlugin.TestCliPlugin).Assembly.Location;

    private static readonly string NativePluginOutputDirectory = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "testing", "Cli.TestNativePlugin", "bin",
#if DEBUG
            "Debug",
#else
            "Release",
#endif
            "net10.0"));

    private readonly string _root = Path.Combine(Path.GetTempPath(), "duende-cli-tests", Path.GetRandomFileName());
    private readonly string _globalPackagesFolder;
    private readonly string _feedFolder;

    public NuGetPluginResolverTests()
    {
        _globalPackagesFolder = Directory.CreateDirectory(Path.Combine(_root, "packages")).FullName;
        _feedFolder = Directory.CreateDirectory(Path.Combine(_root, "feed")).FullName;
    }

    [Fact]
    public async Task when_the_plugin_is_not_cached_expect_it_installs_in_the_nuget_layout_and_loads()
    {
        PublishTestPlugin("1.0.0");

        var assemblyPath = await Resolve(TestPluginId, "1.0.0", Feed());

        var installFolder = PackageFolder(TestPluginId, "1.0.0");
        assemblyPath.ShouldBe(Path.Combine(installFolder, "lib", "net10.0", $"{TestPluginId}.dll"));
        File.Exists(Path.Combine(installFolder, ".nupkg.metadata")).ShouldBeTrue();
        PluginLoader.Load(assemblyPath).Name.ShouldBe("test");
    }

    [Fact]
    public async Task when_nuget_restored_the_plugin_and_the_feed_is_unreachable_expect_it_loads_from_the_cache()
    {
        PublishTestPlugin("1.0.0");
        await RestoreWithNuGet(TestPluginId, "1.0.0");

        var assemblyPath = await Resolve(TestPluginId, "1.0.0", UnreachableFeed());

        PluginLoader.Load(assemblyPath).Name.ShouldBe("test");
    }

    [Fact]
    public async Task when_the_cli_installed_the_plugin_and_nuget_restores_it_later_expect_it_still_loads()
    {
        PublishTestPlugin("1.0.0");
        _ = await Resolve(TestPluginId, "1.0.0", Feed());
        await RestoreWithNuGet(TestPluginId, "1.0.0");

        var assemblyPath = await Resolve(TestPluginId, "1.0.0", UnreachableFeed());

        PluginLoader.Load(assemblyPath).Name.ShouldBe("test");
    }

    [Fact]
    public async Task when_an_older_cli_left_a_flat_folder_expect_it_is_replaced_and_the_plugin_loads()
    {
        PublishTestPlugin("1.0.0");
        var flatFolder = Directory.CreateDirectory(PackageFolder(TestPluginId, "1.0.0")).FullName;
        File.Copy(TestPluginAssemblyPath, Path.Combine(flatFolder, $"{TestPluginId}.dll"));

        var assemblyPath = await Resolve(TestPluginId, "1.0.0", Feed());

        File.Exists(Path.Combine(flatFolder, ".nupkg.metadata")).ShouldBeTrue();
        PluginLoader.Load(assemblyPath).Name.ShouldBe("test");
    }

    [Fact]
    public async Task when_several_versions_are_cached_expect_the_best_match_for_the_range()
    {
        PublishTestPlugin("1.0.0");
        await RestoreWithNuGet(TestPluginId, "1.0.0");
        PublishTestPlugin("1.1.0");
        await RestoreWithNuGet(TestPluginId, "1.1.0");
        PublishTestPlugin("2.0.0");
        await RestoreWithNuGet(TestPluginId, "2.0.0");

        var assemblyPath = await Resolve(TestPluginId, "1.*", UnreachableFeed());

        assemblyPath.ShouldStartWith(PackageFolder(TestPluginId, "1.1.0"));
    }

    [Fact]
    public async Task when_two_cli_runs_install_the_plugin_at_the_same_time_expect_both_load_it()
    {
        PublishTestPlugin("1.0.0");

        var assemblyPaths = await Task.WhenAll(
            Resolve(TestPluginId, "1.0.0", Feed()),
            Resolve(TestPluginId, "1.0.0", Feed()));

        assemblyPaths[1].ShouldBe(assemblyPaths[0]);
        PluginLoader.Load(assemblyPaths[0]).Name.ShouldBe("test");
    }

    [Fact]
    public async Task when_the_plugin_has_native_runtime_assets_expect_it_runs_from_the_nuget_layout()
    {
        PublishNativePlugin("1.0.0");

        var assemblyPath = await Resolve(NativePluginId, "1.0.0", Feed());
        var plugin = PluginLoader.Load(assemblyPath);
        var exitCode = await plugin.GetCommand().Parse([]).InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task when_no_version_on_the_feed_matches_the_range_expect_an_error_naming_the_package_and_range()
    {
        PublishTestPlugin("1.0.0");

        var error = await Should.ThrowAsync<InvalidOperationException>(() => Resolve(TestPluginId, "2.*", Feed()));

        error.Message.ShouldContain(TestPluginId);
        error.Message.ShouldContain("2.*");
        Directory.Exists(new VersionFolderPathResolver(_globalPackagesFolder).GetVersionListPath(TestPluginId)).ShouldBeFalse();
    }

    [Fact]
    public async Task when_a_package_entry_points_outside_its_folder_expect_the_install_is_refused()
    {
        PublishPackageWithEntryOutsideItsFolder("1.0.0", entryPath: "lib/net10.0/../../../../escaped.dll");

        _ = await Should.ThrowAsync<UnsafePackageEntryException>(() => Resolve(TestPluginId, "1.0.0", Feed()));

        Directory.GetFiles(_root, "escaped.dll", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Plugin load contexts keep the loaded assemblies locked until the process ends.
        }
    }

    private Task<string> Resolve(string packageId, string versionRange, SourceRepository source) =>
        NuGetPluginResolver.EnsureDownloadedAsync(
            new PluginResolution(packageId, versionRange),
            _globalPackagesFolder,
            source,
            TestContext.Current.CancellationToken);

    private SourceRepository Feed() => Repository.Factory.GetCoreV3(_feedFolder);

    private SourceRepository UnreachableFeed() => Repository.Factory.GetCoreV3(Path.Combine(_root, "no-feed"));

    private string PackageFolder(string packageId, string version) =>
        new VersionFolderPathResolver(_globalPackagesFolder).GetInstallPath(packageId, NuGetVersion.Parse(version));

    private void PublishTestPlugin(string version) =>
        Publish(TestPluginId, version, [(TestPluginAssemblyPath, $"lib/net10.0/{TestPluginId}.dll")]);

    private void PublishNativePlugin(string version)
    {
        File.Exists(Path.Combine(NativePluginOutputDirectory, $"{NativePluginId}.dll"))
            .ShouldBeTrue($"Plugin not found in {NativePluginOutputDirectory}. Build the Cli.TestNativePlugin project first.");

        var files = Directory.GetFiles(NativePluginOutputDirectory, "*", SearchOption.AllDirectories)
            .Select(file => (file, "lib/net10.0/" + Path.GetRelativePath(NativePluginOutputDirectory, file).Replace('\\', '/')))
            .ToArray();

        Publish(NativePluginId, version, files);
    }

    private void Publish(string packageId, string version, (string Source, string Target)[] files)
    {
        var builder = new PackageBuilder
        {
            Id = packageId,
            Version = NuGetVersion.Parse(version),
            Description = "Test plugin package.",
        };
        _ = builder.Authors.Add("Duende Software");
        builder.TargetFrameworks.Add(NuGetFramework.Parse("net10.0"));
        foreach (var (source, target) in files)
        {
            builder.Files.Add(new PhysicalPackageFile { SourcePath = source, TargetPath = target });
        }

        var packagePath = Path.Combine(_feedFolder, $"{packageId}.{version}.nupkg");
        using var stream = File.Create(packagePath);
        builder.Save(stream);
    }

    private void PublishPackageWithEntryOutsideItsFolder(string version, string entryPath)
    {
        var packagePath = Path.Combine(_feedFolder, $"{TestPluginId}.{version}.nupkg");
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
        using (var nuspec = new StreamWriter(archive.CreateEntry($"{TestPluginId}.nuspec").Open()))
        {
            nuspec.Write(
                $"""
                <?xml version="1.0" encoding="utf-8"?>
                <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                  <metadata>
                    <id>{TestPluginId}</id>
                    <version>{version}</version>
                    <authors>Duende Software</authors>
                    <description>Test plugin package.</description>
                  </metadata>
                </package>
                """);
        }

        _ = archive.CreateEntryFromFile(TestPluginAssemblyPath, entryPath);
    }

    private async Task RestoreWithNuGet(string packageId, string version)
    {
        var packagePath = Path.Combine(_feedFolder, $"{packageId}.{version}.nupkg");
        using var reader = new PackageArchiveReader(packagePath);
        var identity = reader.GetIdentity();
        using var downloader = new LocalPackageArchiveDownloader(_feedFolder, packagePath, identity, NullLogger.Instance);

        _ = await PackageExtractor.InstallFromSourceAsync(
            identity,
            downloader,
            new VersionFolderPathResolver(_globalPackagesFolder),
            new PackageExtractionContext(PackageSaveMode.Defaultv3, XmlDocFileSaveMode.None, clientPolicyContext: null, NullLogger.Instance),
            TestContext.Current.CancellationToken);
    }
}
