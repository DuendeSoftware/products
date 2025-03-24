// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Implementation of ISigningKeyStore based on file system.
/// </summary>
public class FileSystemKeyStore : ISigningKeyStore
{
    private const string KeyFilePrefix = "is-signing-key-";
    private const string KeyFileExtension = ".json";

    private readonly DirectoryInfo _directory;
    private readonly ILogger<FileSystemKeyStore> _logger;

    /// <summary>
    /// Constructor for FileSystemKeyStore.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="logger"></param>
    public FileSystemKeyStore(string path, ILogger<FileSystemKeyStore> logger)
        : this(new DirectoryInfo(path), logger)
    {
    }

    /// <summary>
    /// Constructor for FileSystemKeyStore.
    /// </summary>
    /// <param name="directory"></param>
    /// <param name="logger"></param>
    public FileSystemKeyStore(DirectoryInfo directory, ILogger<FileSystemKeyStore> logger)
    {
        _directory = directory;
        _logger = logger;
    }

    /// <summary>
    /// Returns all the keys in storage.
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<SerializedKey>> LoadKeysAsync()
    {
        var list = new List<SerializedKey>();

        if (!_directory.Exists)
        {
            _directory.Create();
        }

        var files = _directory.GetFiles(KeyFilePrefix + "*" + KeyFileExtension);
        foreach (var file in files)
        {
            var id = file.Name.Substring(4);
            try
            {
                using (var reader = new StreamReader(file.OpenRead()))
                {
                    var json = await reader.ReadToEndAsync();
                    var item = KeySerializer.Deserialize<SerializedKey>(json);
                    list.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading file: " + file.Name);
            }
        }

        return list;
    }

    /// <summary>
    /// Persists new key in storage.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public Task StoreKeyAsync(SerializedKey key)
    {
        if (!_directory.Exists)
        {
            _directory.Create();
        }

        var json = KeySerializer.Serialize(key);

        var path = Path.Combine(_directory.FullName, KeyFilePrefix + key.Id + KeyFileExtension);
        File.WriteAllText(path, json, Encoding.UTF8);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes key from storage.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Task DeleteKeyAsync(string id)
    {
        var path = Path.Combine(_directory.FullName, KeyFilePrefix + id + KeyFileExtension);
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: " + path);
        }

        return Task.CompletedTask;
    }
}
