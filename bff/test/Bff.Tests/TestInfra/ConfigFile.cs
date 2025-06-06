// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Bff.Configuration;

namespace Duende.Bff.Tests.TestInfra;

internal class ConfigFile : IDisposable
{
    private IConfiguration? _configuration { get; set; }

    public ConfigFile()
    {
        var dir = Path.GetDirectoryName(_file)!;
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public void Save(BffConfiguration config)
    {
        File.WriteAllText(ToString(), JsonSerializer.Serialize(config));

        if (_configuration == null)
        {
            _configuration = new ConfigurationBuilder()
                .AddJsonFile(ToString(), false, true)
                .Build();
        }
        else
        {
            ((IConfigurationRoot)_configuration).Reload();
        }
    }

    public IConfiguration Configuration => _configuration ?? throw new InvalidOperationException("File not initialized");

    private readonly string _file = Path.GetTempFileName();

    public override string ToString() => _file;


    public void Dispose()
    {
        if (File.Exists(_file))
        {
            try
            {
                // Do your best to delete the file. 
                File.Delete(_file);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
