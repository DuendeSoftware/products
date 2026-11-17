// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;

namespace Duende.UserManagement;
/// <summary>
/// Registry for DSO type registration.
/// </summary>
internal sealed class FakeDsoTypeRegistry
{
    private readonly Dictionary<DataStorageObjectVersion, Type> _registeredTypes = new();

    /// <summary>
    /// Registers a DSO type in the registry.
    /// </summary>
    /// <typeparam name="TDso">The DSO type to register.</typeparam>
    internal void Register<TDso>() where TDso : IDataStorageObject => _registeredTypes[TDso.DsoVersion] = typeof(TDso);

    /// <summary>
    /// Gets the registered type for the specified DSO version.
    /// </summary>
    /// <param name="dsoVersion">The DSO version to look up.</param>
    /// <returns>The registered type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the dsoVersion is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the DSO type is not registered.</exception>
    internal Type GetRegisteredType(DataStorageObjectVersion dsoVersion)
    {
        ArgumentNullException.ThrowIfNull(dsoVersion);
        _ = _registeredTypes.TryGetValue(dsoVersion, out var registeredType);
        return registeredType ?? throw new InvalidOperationException($"DsoType {dsoVersion.EntityType.Name} with version {dsoVersion.SchemaVersion} is not registered.");
    }

    public static implicit operator GetRegisteredTypeForDso(FakeDsoTypeRegistry registry) => registry.GetRegisteredType;
}
