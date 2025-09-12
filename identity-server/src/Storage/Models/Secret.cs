// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models a client secret with identifier and expiration
/// </summary>
public class Secret
{
    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    /// <value>
    /// The description.
    /// </value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    /// <value>
    /// The value.
    /// </value>
    public string Value { get; set; } = default!;

    /// <summary>
    /// Gets or sets the expiration.
    /// </summary>
    /// <value>
    /// The expiration.
    /// </value>
    public DateTime? Expiration { get; set; }

    /// <summary>
    /// Gets or sets the type of the client secret.
    /// </summary>
    /// <value>
    /// The type of the client secret.
    /// </value>
    public string Type { get; set; } = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="Secret"/> class.
    /// </summary>
    public Secret() => Type = IdentityServerConstants.SecretTypes.SharedSecret;

    /// <summary>
    /// Initializes a new instance of the <see cref="Secret"/> class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="expiration">The expiration.</param>
    public Secret(string value, DateTime? expiration = null)
        : this()
    {
        Value = value;
        Expiration = expiration;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Secret" /> class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="description">The description.</param>
    /// <param name="expiration">The expiration.</param>
    public Secret(string value, string description, DateTime? expiration = null)
        : this()
    {
        Description = description;
        Value = value;
        Expiration = expiration;
    }

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>
    /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
    /// </returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 23) + (Value?.GetHashCode(StringComparison.InvariantCulture) ?? 0);
            hash = (hash * 23) + (Type?.GetHashCode(StringComparison.InvariantCulture) ?? 0);

            return hash;
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="object" />, is equal to this instance.
    /// </summary>
    /// <param name="obj">The <see cref="object" /> to compare with this instance.</param>
    /// <returns>
    ///   <c>true</c> if the specified <see cref="object" /> is equal to this instance; otherwise, <c>false</c>.
    /// </returns>
    public override bool Equals(object? obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (obj is not Secret other)
        {
            return false;
        }

        if (ReferenceEquals(other, this))
        {
            return true;
        }

        return string.Equals(other.Type, Type, StringComparison.Ordinal) &&
               string.Equals(other.Value, Value, StringComparison.Ordinal);
    }
}
