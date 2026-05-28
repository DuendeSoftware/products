// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Text.Json.Serialization;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models name for a scheme
/// </summary>
public class IdentityProviderName
{
    /// <summary>
    /// Gets or sets the scheme name for the provider.
    /// </summary>
    public string Scheme { get; set; } = default!;

    /// <summary>
    /// Gets or sets the display name for the provider.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this provider is enabled and can be used for authentication. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Models general storage for an external authentication provider/handler scheme
/// </summary>
public record IdentityProvider
{
    /// <summary>
    /// Ctor
    /// </summary>
    [JsonConstructor]
    public IdentityProvider(string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        Type = type;
    }

    /// <summary>
    /// Ctor
    /// </summary>
    public IdentityProvider(string type, IdentityProvider other) : this(type)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other.Type != type)
        {
            throw new ArgumentException($"Type '{type}' does not match type of other '{other.Type}'");
        }

        Scheme = other.Scheme;
        DisplayName = other.DisplayName;
        Enabled = other.Enabled;
        Type = other.Type;
        Properties = new Dictionary<string, string>(other.Properties);
    }

    /// <summary>
    /// Gets or sets the scheme name for the provider.
    /// </summary>
    public string Scheme { get; set; } = default!;

    /// <summary>
    /// Gets or sets the display name for the provider.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this provider is enabled and can be used for authentication. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the protocol type of the provider (e.g. <c>oidc</c> for OpenID Connect). Used to distinguish provider implementations.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the protocol-specific properties for the provider, stored as a key-value dictionary.
    /// Derived classes (e.g. <see cref="OidcProvider"/>) expose typed properties backed by this dictionary.
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Properties indexer
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    protected string? this[string name]
    {
        get
        {
            Properties.TryGetValue(name, out var result);
            return result;
        }
        set => Properties[name] = value!;
    }

    /// <summary>
    /// Provides value-based equality that includes the <see cref="Properties"/> dictionary contents.
    /// The compiler-generated record equality uses reference equality for the dictionary,
    /// so we override to compare entries by value.
    /// </summary>
    public virtual bool Equals(IdentityProvider? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (EqualityContract != other.EqualityContract)
        {
            return false;
        }

        return Scheme == other.Scheme
            && DisplayName == other.DisplayName
            && Enabled == other.Enabled
            && Type == other.Type
            && DictionariesEqual(Properties, other.Properties);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EqualityContract);
        hash.Add(Scheme);
        hash.Add(DisplayName);
        hash.Add(Enabled);
        hash.Add(Type);

        foreach (var kvp in Properties.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key, StringComparer.Ordinal);
            hash.Add(kvp.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private static bool DictionariesEqual(Dictionary<string, string> a, Dictionary<string, string> b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
            {
                return false;
            }
        }

        return true;
    }
}
