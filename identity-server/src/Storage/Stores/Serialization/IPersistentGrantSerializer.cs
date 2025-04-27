// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Stores.Serialization;

/// <summary>
/// Interface for persisted grant serialization
/// </summary>
public interface IPersistentGrantSerializer
{
    /// <summary>
    /// Serializes the specified value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value">The value.</param>
    string Serialize<T>(T value);

    /// <summary>
    /// Deserializes the specified string.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="json">The json.</param>
    T Deserialize<T>(string json);
}
