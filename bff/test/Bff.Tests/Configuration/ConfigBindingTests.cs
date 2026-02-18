// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Tests.Configuration;

public class OptionsMonitorExplorationTests
{
    /// <summary>
    /// I found a problematic / inconsistent situation with binding two classes to the same IConfiguration and
    /// how this then updates the IOptionsMonitor instances.
    ///
    /// Reported this as an issue in the .net repo:
    /// https://github.com/dotnet/runtime/issues/119883
    /// </summary>
    [Fact]
    public void OptionMonitorsAreUpdatedAsynchronously()
    {
        var builder = Host.CreateApplicationBuilder();

        var provider = new CustomConfigurationProvider();

        // Bind the configuration to the CustomConfigurationProvider
        var customConfigurationSource = new CustomConfigurationSource(provider);
        builder.Configuration.Sources.Add(customConfigurationSource);

        // Important: I'm binding two classes to the same IConfiguration instance
        // The different classes are mapped to the same data.
        builder.Services.Configure<MyConfig>(builder.Configuration);
        builder.Services.Configure<MyConfig2>(builder.Configuration);

        using var host = builder.Build();

        // Get a monitor for both classes.
        var monitor1 = host.Services.GetRequiredService<IOptionsMonitor<MyConfig>>();
        var monitor2 = host.Services.GetRequiredService<IOptionsMonitor<MyConfig2>>();

        // Count for each data what the number of items is when the first monitor changes.
        var countMyConfig1 = 0;
        var countMyConfig2 = 0;
        monitor1.OnChange(c =>
        {
            countMyConfig1 = c.Items.Count;
            countMyConfig2 = monitor2.CurrentValue.Items.Count;
        });

        // Now we load a single data item and trigger a (single) reload.
        provider.LoadDataWithOneItem();
        provider.Reload();
        Task.Delay(200);

        // After first reload both monitors have 1 item.
        countMyConfig1.ShouldBe(1);
        countMyConfig2.ShouldBe(1);

        // Now reload the configuration, but with two items
        provider.LoadDataWithSecondItem();
        provider.Reload();
        Task.Delay(200);

        // The first config is updated (as expected)
        countMyConfig1.ShouldBe(2);

        // But the second config is NOT updated (not expected)
        // The second options monitor isn't updated. This behavior
        // is different from the initial load (which has both IOptionMonitors updated)
        // or the first reload (which also updates both IOptionsMonitors)
        countMyConfig2.ShouldBe(1, "I would have expected this to be 2");

        // A second reload now causes both providers to be reloaded.
        customConfigurationSource.Provider.Reload();

        Task.Delay(200);

        // and now the options monitors for BOTH are updated.
        countMyConfig1.ShouldBe(2);
        countMyConfig2.ShouldBe(2);
    }


    public class CustomConfigurationSource(CustomConfigurationProvider provider) : IConfigurationSource
    {
        public CustomConfigurationProvider Provider { get; set; } = provider;

        public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
    }

    public class CustomConfigurationProvider : ConfigurationProvider
    {
        public void LoadDataWithOneItem()
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            Data["Items:Item1:Name"] = "FirstSet_Item1";
            Data["Items:Item1:Name"] = "FirstSet_Item1";
        }

        public void LoadDataWithSecondItem()
        {
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            Data["Items:Item1:Name"] = "FirstSet_Item1";
            Data["Items:Item1:Name"] = "FirstSet_Item1";
            Data["Items:Item2:Name"] = "FirstSet_Item2";
            Data["Items:Item2:Name"] = "FirstSet_Item2";
        }

        public void Reload() => OnReload();

        public override void Load()
        {

        }
    }

    public class MyConfig
    {
        public Dictionary<string, MyConfigItem> Items { get; set; } = new();
    }

    public class MyConfig2 : MyConfig
    {
    }

    public class MyConfigItem
    {
        public string Name { get; set; } = null!;
    }

}

public class ConfigBindingTests : BffTestBase
{
    private readonly ITestOutputHelper _output = TestContext.Current.TestOutputHelper!;


    /// <summary>
    /// This test Highlights a problem when loading configuration from multiple files
    ///
    /// I was testing loading configuration from multiple files. I did this by renaming
    /// a file to and from .json to .json_ to trigger the file watcher.
    ///
    /// Strangely enough, under some situations, the RemoteApi's for the second frontend
    /// weren't loaded. After quite some investigation, I found that this is due to
    /// the IOptionsMonitor for the second frontend not being updated when the configuration
    /// changes. This is inconsistent behavior, because the first frontend's IOptionsMonitor
    ///
    /// the underlying issue is this one:
    /// https://github.com/dotnet/runtime/issues/119883
    ///
    /// A workaround is implemented, which means loading the data directly from a single configuration provider.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task Can_load_remote_apis_at_runtime_from_multiple_config_sources()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(folder);




        var config = new ConfigurationBuilder()
            .Add(new MergedJsonConfigurationSource(folder, _output))
            .Build();
        await File.WriteAllTextAsync(Path.Combine(folder, "app1.json_"), """
                                                                        {
                                                                          "frontends": {
                                                                            "app1": {
                                                                              "StaticAssetsUrl": "https://localhost:5173/",
                                                                              "MatchingHostHeader": "app1.localhost:7255",
                                                                              "remoteApis": [
                                                                                {
                                                                                  "pathMatch": "/api1",
                                                                                  "targetUri": "https://localhost:7200/",
                                                                                  "requiredTokenType": "None"
                                                                                },
                                                                                {
                                                                                  "pathMatch": "/api2",
                                                                                  "targetUri": "https://localhost:7085/",
                                                                                  "requiredTokenType": "None"
                                                                                }
                                                                              ]
                                                                            }
                                                                          }
                                                                        }
                                                                        """);

        await File.WriteAllTextAsync(Path.Combine(folder, "app2.json_"), """
                                                                        {
                                                                          "frontends": {
                                                                            "app2": {
                                                                              "StaticAssetsUrl": "https://localhost:5173/",
                                                                              "MatchingHostHeader": "app1.localhost:7255",
                                                                              "remoteApis": [
                                                                                {
                                                                                  "pathMatch": "/api1",
                                                                                  "targetUri": "https://localhost:7200/",
                                                                                  "requiredTokenType": "None"
                                                                                },
                                                                                {
                                                                                  "pathMatch": "/api2",
                                                                                  "targetUri": "https://localhost:7085/",
                                                                                  "requiredTokenType": "None"
                                                                                }
                                                                              ]
                                                                            }
                                                                          }
                                                                        }
                                                                        """);

        Bff.OnConfigureBff += bff =>
        {
            bff.LoadConfiguration(config);

            bff.AddRemoteApis();

        };

        await InitializeAsync();

        File.Move(Path.Combine(folder, "app1.json_"), Path.Combine(folder, "app1.json"));
        await Task.Delay(2000);

        File.Move(Path.Combine(folder, "app2.json_"), Path.Combine(folder, "app2.json"));
        await Task.Delay(200);

        var frontends = Bff.Resolve<IFrontendCollection>();

        frontends.Count.ShouldBe(2);
        frontends.First().GetRemoteApis().Length.ShouldBe(2);
        frontends.Skip(1).First().GetRemoteApis().Length.ShouldBe(2);

        await DisposeAsync();

        try
        {
            Directory.Delete(folder, true);
        }
        catch (Exception)
        {
        }
    }
}



/// <summary>
/// A custom configuration source that reads all .json files from a directory,
/// merges them, and watches for changes.
/// </summary>
public class MergedJsonConfigurationSource(string directoryPath, ITestOutputHelper output) : IConfigurationSource
{
    public string DirectoryPath { get; } = directoryPath;

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new MergedJsonConfigurationProvider(output, this);
}
public class MergedJsonConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly MergedJsonConfigurationSource _source;
    private readonly FileSystemWatcher _watcher;

    public MergedJsonConfigurationProvider(ITestOutputHelper testOutputHelper, MergedJsonConfigurationSource source)
    {
        _testOutputHelper = testOutputHelper;
        _source = source;
        _watcher = new FileSystemWatcher(_source.DirectoryPath)
        {
            Filter = "*.json",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
    }

    public override void Load() => Load(reload: false);

    private void OnFileRenamed(object sender, RenamedEventArgs e) => Load(reload: true);

    private void OnFileChanged(object sender, FileSystemEventArgs e) => Load(reload: true);

    private void Load(bool reload)
    {
        JsonElement? mergedConfig = null;

        try
        {
            var files = Directory.GetFiles(_source.DirectoryPath, "*.json", SearchOption.TopDirectoryOnly);

            foreach (var file in files.OrderBy(f => f))
            {
                try
                {
                    var jsonContent = File.ReadAllText(file);
                    if (string.IsNullOrWhiteSpace(jsonContent))
                    {
                        continue;
                    }

                    using var doc = JsonDocument.Parse(jsonContent);
                    mergedConfig = mergedConfig == null
                        ? doc.RootElement.Clone()
                        : MergeJsonElements(mergedConfig.Value, doc.RootElement);
                }
                catch (Exception ex)
                {
                    _testOutputHelper.WriteLine($"[Warning] Could not read or parse config file '{Path.GetFileName(file)}'. Skipping. Error: {ex.Message}");
                }
            }

            Data = mergedConfig != null
                ? FlattenJsonElement(mergedConfig.Value)
                : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _testOutputHelper.WriteLine($"[Error] Could not load merged configuration. Error: {ex.Message}");
            Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        if (reload)
        {
            OnReload();
        }
    }

    // Deep merge two JsonElements (objects and arrays)
    private static JsonElement MergeJsonElements(JsonElement baseElement, JsonElement overrideElement)
    {
        if (baseElement.ValueKind == JsonValueKind.Object && overrideElement.ValueKind == JsonValueKind.Object)
        {
            var merged = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in baseElement.EnumerateObject())
            {
                merged[prop.Name] = prop.Value.Clone();
            }

            foreach (var prop in overrideElement.EnumerateObject())
            {
                if (merged.TryGetValue(prop.Name, out var existing))
                {
                    merged[prop.Name] = MergeJsonElements(existing, prop.Value);
                }
                else
                {
                    merged[prop.Name] = prop.Value.Clone();
                }
            }

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(merged.ToDictionary(kv => kv.Key, kv => kv.Value)));
            return doc.RootElement.Clone();
        }
        else if (baseElement.ValueKind == JsonValueKind.Array && overrideElement.ValueKind == JsonValueKind.Array)
        {
            // Union arrays
            var baseArray = baseElement.EnumerateArray().ToList();
            var overrideArray = overrideElement.EnumerateArray().ToList();
            var union = baseArray.Concat(overrideArray).ToList();

            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(union));
            return doc.RootElement.Clone();
        }
        else
        {
            // Override primitive or mismatched types
            return overrideElement.Clone();
        }
    }

    private static IDictionary<string, string?> FlattenJsonElement(JsonElement element)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        FillDictionaryFromJsonElement(dict, element, string.Empty);
        return dict;
    }

    private static void FillDictionaryFromJsonElement(IDictionary<string, string?> dict, JsonElement element, string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    FillDictionaryFromJsonElement(dict, prop.Value, BuildPrefix(prefix, prop.Name));
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var value in element.EnumerateArray())
                {
                    FillDictionaryFromJsonElement(dict, value, BuildPrefix(prefix, index.ToString()));
                    index++;
                }
                break;
            case JsonValueKind.Null:
                break;
            default:
                dict[prefix] = element.ToString();
                break;
        }
    }

    private static string BuildPrefix(string prefix, string key) =>
        string.IsNullOrEmpty(prefix) ? key : $"{prefix}{ConfigurationPath.KeyDelimiter}{key}";

    public void Dispose() => _watcher?.Dispose();


}
