// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Diagnostics.CodeAnalysis;
using static Duende.IdentityServer.IdentityServerConstants;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options to configure behavior of KeyManager.
/// </summary>
public class KeyManagementOptions
{
    /// <summary>
    /// Specifies if key management should be enabled. Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Key size (in bits) of RSA keys. Defaults to 2048.
    /// </summary>
    public int RsaKeySize { get; set; } = 2048;

    /// <summary>
    /// The signing algorithms allowed. 
    /// If none are specified, then "RS256" will be used as the default.
    /// The first in the collection will be used as the default. 
    /// </summary>
    public ICollection<SigningAlgorithmOptions> SigningAlgorithms { get; set; } = new List<SigningAlgorithmOptions>();

    internal string DefaultSigningAlgorithm => SigningAlgorithms.First().Name;
    internal IEnumerable<string> AllowedSigningAlgorithmNames => SigningAlgorithms.Select(x => x.Name);

    /// <summary>
    /// When no keys have been created yet, this is the window of time considered to be an initialization 
    /// period to allow all servers to synchronize if the keys are being created for the first time.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan InitializationDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Delay used when re-loading from the store when the initialization period. It allows
    /// other servers more time to write new keys so other servers can include them.
    /// Defaults to 5 seconds.
    /// </summary>
    public TimeSpan InitializationSynchronizationDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Cache duration when within the initialization period.
    /// Defaults to 1 minute.
    /// </summary>
    public TimeSpan InitializationKeyCacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// When in normal operation, duration to cache keys from store.
    /// Defaults to 24 hours.
    /// </summary>
    public TimeSpan KeyCacheDuration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Time expected to propagate new keys to all servers, and time expected all clients to refresh discovery.
    /// Defaults to 14 days.
    /// </summary>
    public TimeSpan PropagationTime { get; set; } = TimeSpan.FromDays(14);

    /// <summary>
    /// Age at which keys will no longer be used for signing, but will still be used in discovery for validation.
    /// Defaults to 90 days.
    /// </summary>
    public TimeSpan RotationInterval { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Duration for keys to remain in discovery after rotation.
    /// Defaults to 14 days.
    /// </summary>
    public TimeSpan RetentionDuration { get; set; } = TimeSpan.FromDays(14);


    internal TimeSpan KeyRetirementAge => RotationInterval + RetentionDuration;


    /// <summary>
    /// Automatically delete retired keys.
    /// Defaults to true.
    /// </summary>
    public bool DeleteRetiredKeys { get; set; } = true;

    /// <summary>
    /// Automatically protect keys in the storage using data protection.
    /// Defaults to true.
    /// </summary>
    public bool DataProtectKeys { get; set; } = true;

    /// <summary>
    /// Path for storing keys when using the default file system store.
    /// Defaults to the "keys" directory relative to the hosting application.
    /// </summary>
    public string KeyPath { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "keys");

    internal void Validate()
    {
        if (SigningAlgorithms?.Any() != true)
        {
            SigningAlgorithms = new[] { new SigningAlgorithmOptions("RS256") };
        }
        else
        {
            var group = SigningAlgorithms.GroupBy(x => x.Name);
            var dups = group.Where(x => x.Count() > 1);
            if (dups.Any())
            {
                var names = dups.Select(x => x.Key).Aggregate((x, y) => $"{x}, {y}");
                throw new Exception($"Duplicate signing algorithms not allowed: '{names}'.");
            }
        }

        var invalid = AllowedSigningAlgorithmNames.Where(x => !SupportedSigningAlgorithms.Contains(x)).ToArray();
        if (invalid.Any())
        {
            var values = invalid.Aggregate((x, y) => $"{x}, {y}");
            throw new Exception($"Invalid signing algorithm(s): '{values}'.");
        }

        var invalidEcKeys = SigningAlgorithms.Where(x => x.IsEcKey && x.UseX509Certificate).ToArray();
        if (invalidEcKeys.Any())
        {
            var values = invalidEcKeys.Select(x => x.Name).Aggregate((x, y) => $"{x}, {y}");
            throw new Exception($"UseX509Certificate not currently supported for EC keys. Signing algorithm(s): '{values}'.");
        }

        if (InitializationDuration < TimeSpan.Zero) throw new Exception(nameof(InitializationDuration) + " must be greater than or equal to zero.");
        if (InitializationSynchronizationDelay < TimeSpan.Zero) throw new Exception(nameof(InitializationSynchronizationDelay) + " must be greater than or equal to zero.");

        if (InitializationKeyCacheDuration < TimeSpan.Zero) throw new Exception(nameof(InitializationKeyCacheDuration) + " must be greater than or equal to zero.");
        if (KeyCacheDuration < TimeSpan.Zero) throw new Exception(nameof(KeyCacheDuration) + " must be greater than or equal to zero.");

        if (PropagationTime <= TimeSpan.Zero) throw new Exception(nameof(PropagationTime) + " must be greater than zero.");
        if (RotationInterval <= TimeSpan.Zero) throw new Exception(nameof(RotationInterval) + " must be greater than zero.");
        if (RetentionDuration <= TimeSpan.Zero) throw new Exception(nameof(RetentionDuration) + " must be greater than zero.");

        if (KeyCacheDuration > PropagationTime / 2)
        {
            // we should not cache too long, because we need a server to have latest data
            // to allow clients/apis time to update their caches.
            // todo: error, or just calculate it?
            KeyCacheDuration = PropagationTime / 2;
        }

        if (RotationInterval <= PropagationTime) throw new Exception(nameof(RotationInterval) + " must be longer than " + nameof(PropagationTime));
    }
}

/// <summary>
/// Class to configure signing algorithm.
/// </summary>
public class SigningAlgorithmOptions
{
    /// <summary>
    /// Parameterless constructor, required for binding
    /// </summary>
    public SigningAlgorithmOptions()
    {

    }

    /// <summary>
    /// Constructor.
    /// </summary>
    [SetsRequiredMembers]
    public SigningAlgorithmOptions(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// The algorithm name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Indicates if an X.509 certificate is to be used to contain the key. Defaults to false.
    /// </summary>
    public bool UseX509Certificate { get; set; }

    internal bool IsRsaKey => Name.StartsWith('R') || Name.StartsWith('P');
    internal bool IsEcKey => Name.StartsWith('E');
}
